using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.Ports.ChangeQuote;
using AeroTech.Ordering.Domain.Ports.DocumentChangeEligibility;
using AeroTech.Ordering.Domain.Ports.DocumentRevalidation;
using AeroTech.Ordering.Domain.Ports.ReservationChange;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange
{
    public sealed class VoluntaryChangeService : IVoluntaryChangeService
    {
        public const string QuoteStep = "accept-quoted-change";
        public const string ReservationStep = "reservation-change";
        public const string EligibilityStep = "document-change-eligibility";
        public const string RevalidationStep = "document-revalidate";

        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IChangeQuotePort _quotes;
        private readonly IReservationChangePort _reservations;
        private readonly IDocumentChangeEligibilityPort _eligibility;
        private readonly IDocumentRevalidationPort _revalidation;
        private readonly IAcceptedChangePlanStore _plans;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly ICallerContext _callerContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public VoluntaryChangeService(
            IOrderRepository orders,
            IElectronicTicketRepository tickets,
            IChangeQuotePort quotes,
            IReservationChangePort reservations,
            IDocumentChangeEligibilityPort eligibility,
            IDocumentRevalidationPort revalidation,
            IAcceptedChangePlanStore plans,
            IOrderOperationCoordinator operations,
            IServicingOperationStore operationStore,
            ICommandReceiptStore receipts,
            ICallerContext callerContext,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector)
        {
            _orders = orders;
            _tickets = tickets;
            _quotes = quotes;
            _reservations = reservations;
            _eligibility = eligibility;
            _revalidation = revalidation;
            _plans = plans;
            _operations = operations;
            _operationStore = operationStore;
            _receipts = receipts;
            _callerContext = callerContext;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<ChangeQuoteOutcome> QuoteAsync(
            long orderId,
            long orderServiceId,
            CancellationToken cancellationToken = default)
        {
            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var service = order.RequireChangeableAirService(orderServiceId);
            var (ticket, coupon) = await ResolveDocumentAsync(orderId, service.Id, cancellationToken);

            ticket.EnsureCouponCanBeRevalidated(coupon.Id, service.Id);

            var quote = await _quotes.QuoteAsync(
                new ChangeQuoteRequest(
                    order.Id,
                    order.CommercialVersion,
                    ticket.Id,
                    service.Id,
                    coupon.Id,
                    order.CurrencyId),
                cancellationToken);

            ChangeQuoteBinding.EnsureQuoteBindsToTheOrder(quote, order, ticket, service.Id, coupon.Id);

            return new ChangeQuoteOutcome(
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                ticket.DocumentNumber,
                service.Id,
                coupon.Id,
                quote.QuotedChangeId,
                quote.SourceSystem,
                quote.TargetSelectionRef,
                quote.MonetaryOutcome,
                quote.SaleCurrencyId,
                quote.ExpiresAt,
                quote.ContinuedOrderServiceIds,
                quote.Replacement,
                quote.SourcePricingReference);
        }

        public async Task<VoluntaryChangeOutcome> ChangeAsync(
            VoluntaryChangeExecution execution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            ArgumentException.ThrowIfNullOrWhiteSpace(execution.IdempotencyKey);

            if (string.IsNullOrWhiteSpace(execution.QuotedChangeId))
                throw ExceptionFactory.OrderChangeRequiresQuote(execution.OrderId);

            if (execution.ExpectedCommercialVersion is null)
                throw ExceptionFactory.ExpectedCommercialVersionRequired(execution.OrderId);

            var order = await _orders.GetAsync(execution.OrderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(execution.OrderId);

            var operation = await _operations.BeginAsync(
                execution.OrderId,
                ServicingOperationKind.Revalidate,
                execution.IdempotencyKey,
                new
                {
                    Operation = "VoluntaryChange",
                    OrderId = execution.OrderId,
                    OrderServiceId = execution.OrderServiceId,
                    QuotedChangeId = execution.QuotedChangeId,
                    ExpectedCommercialVersion = execution.ExpectedCommercialVersion
                },
                execution.ExpectedCommercialVersion,
                cancellationToken);

            var committed = CommittedChange(order, operation.OperationId);

            if (committed is not null)
                return await ReplayFinalizedAsync(order, operation, committed, cancellationToken);

            var plan = await _plans.FindAsync(operation.OperationId, cancellationToken);

            if (operation.IsReplay && plan is not null)
            {
                var unfinished = await ReplayUnfinishedAsync(order, operation, plan, cancellationToken);

                if (unfinished is not null)
                    return unfinished;
            }

            return await ExecuteFreshAsync(order, operation, execution, cancellationToken);
        }

        private async Task<VoluntaryChangeOutcome> ExecuteFreshAsync(
            Order order,
            OrderOperation operation,
            VoluntaryChangeExecution execution,
            CancellationToken cancellationToken)
        {
            ElectronicTicket ticket;
            Entities.OrderService service;
            AcceptedChangePlan plan;

            try
            {
                if (execution.ExpectedCommercialVersion != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(
                        execution.ExpectedCommercialVersion,
                        execution.OrderId,
                        order.CommercialVersion);

                service = order.RequireChangeableAirService(execution.OrderServiceId);

                var resolved = await ResolveDocumentAsync(execution.OrderId, service.Id, cancellationToken);

                ticket = resolved.Ticket;

                ticket.EnsureCouponCanBeRevalidated(resolved.Coupon.Id, service.Id);
                order.EnsureNoActiveServiceDependsOn(service.Id);

                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.Executing,
                    operation.ClaimGeneration,
                    cancellationToken);

                var accepted = await _quotes.AcceptQuotedChangeAsync(
                    new AcceptedQuotedChangeSelection(
                        _operations.ProviderOperationKey(operation, QuoteStep),
                        order.Id,
                        operation.OperationId,
                        execution.QuotedChangeId!,
                        execution.ExpectedCommercialVersion!.Value,
                        ticket.Id,
                        service.Id,
                        resolved.Coupon.Id,
                        order.CurrencyId),
                    cancellationToken);

                ChangeQuoteBinding.EnsureAcceptedBindsToTheRequest(
                    accepted,
                    order,
                    ticket,
                    service.Id,
                    resolved.Coupon.Id,
                    execution.QuotedChangeId!,
                    execution.ExpectedCommercialVersion.Value,
                    _clock.GetDateTime());

                if (accepted.MonetaryOutcome != ChangeMonetaryOutcome.Even)
                    return await DeferToExchangeAsync(
                        order, operation, ticket, accepted.MonetaryOutcome, cancellationToken);

                plan = new AcceptedChangePlan(
                    operation.OperationId,
                    order.Id,
                    accepted.QuotedChangeId,
                    accepted.SourceSystem,
                    accepted.TargetSelectionRef,
                    ticket.Id,
                    service.Id,
                    _idGenerator.NewId(),
                    _idGenerator.NewId(),
                    resolved.Coupon.Id,
                    execution.ExpectedCommercialVersion.Value,
                    accepted.MonetaryOutcome,
                    accepted);

                order.PrepareVoluntaryChange(ToArgs(plan), _idGenerator, _clock);

                var eligibility = await _eligibility.EvaluateAsync(
                    new DocumentChangeEligibilityRequest(
                        _operations.ProviderOperationKey(operation, EligibilityStep),
                        order.Id,
                        operation.OperationId,
                        ticket.DocumentNumber,
                        resolved.Coupon.Id,
                        resolved.Coupon.CouponNumber,
                        accepted.TargetSelectionRef,
                        accepted.MonetaryOutcome),
                    cancellationToken);

                if (eligibility.Outcome != DocumentChangeEligibilityOutcome.Revalidate)
                    return await SettleEligibilityAsync(order, operation, ticket, eligibility, cancellationToken);

                await _plans.SaveAsync(plan, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await TryReleaseRejectedAsync(execution.OrderId, operation, cancellationToken);
                throw;
            }

            return await ApplyReservationChangeAsync(order, operation, ticket, plan, false, cancellationToken);
        }

        private async Task<VoluntaryChangeOutcome> ApplyReservationChangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedChangePlan plan,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            ReservationChangeResult result;

            try
            {
                result = await _reservations.ApplyAsync(
                    new ReservationChangeRequest(
                        ReservationKey(operation, plan),
                        order.Id,
                        operation.OperationId,
                        null,
                        plan.ReplacedOrderServiceId,
                        plan.ReplacementOrderServiceId,
                        plan.ReplacementOrderSegmentId,
                        plan.Accepted.Replacement.Segment.CapacityReference,
                        plan.Accepted.Replacement.Segment.BookingClass,
                        plan.Accepted.Replacement.BeneficiaryTravellerIds[0]),
                    cancellationToken);
            }
            catch
            {
                await TryReleaseRejectedAsync(order.Id, operation, cancellationToken);
                throw;
            }

            return await AfterReservationAsync(order, operation, ticket, plan, result, isReplay, cancellationToken);
        }

        private async Task<VoluntaryChangeOutcome> AfterReservationAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedChangePlan plan,
            ReservationChangeResult result,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            await _plans.RecordReservationOutcomeAsync(
                operation.OperationId, result.Outcome, result.ExternalReservationRef, cancellationToken);

            if (result.Outcome == ProviderOperationOutcome.Rejected)
                return await SettleAsync(
                    order, operation, ticket, plan,
                    ServicingOperationStatus.Rejected, CommandReceiptStatus.Rejected,
                    result.Outcome, ProviderOperationOutcome.Pending,
                    releaseClaim: true, ChangeDocumentOutcome.NotAttempted, isReplay, cancellationToken);

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return await SettleAsync(
                    order, operation, ticket, plan,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    result.Outcome, ProviderOperationOutcome.Pending,
                    releaseClaim: false, ChangeDocumentOutcome.NotAttempted, isReplay, cancellationToken);

            return await RevalidateAsync(order, operation, ticket, plan, false, isReplay, cancellationToken);
        }

        private async Task<VoluntaryChangeOutcome> RevalidateAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedChangePlan plan,
            bool recoverFirst,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            DocumentRevalidationResult result;

            try
            {
                result = recoverFirst
                    ? await _revalidation.RecoverAsync(
                        new DocumentRevalidationRecoveryRequest(
                            RevalidationKey(operation, ticket),
                            order.Id,
                            operation.OperationId,
                            ticket.DocumentNumber),
                        cancellationToken)
                    : await _revalidation.RevalidateAsync(
                        new DocumentRevalidationRequest(
                            RevalidationKey(operation, ticket),
                            order.Id,
                            operation.OperationId,
                            ticket.DocumentNumber,
                            plan.TicketCouponId,
                            ticket.CouponFor(plan.TicketCouponId).CouponNumber,
                            plan.TargetSelectionRef),
                        cancellationToken);
            }
            catch
            {
                await TryReleaseRejectedAsync(order.Id, operation, cancellationToken);
                throw;
            }

            if (result.Outcome == ProviderOperationOutcome.Confirmed)
                return await FinalizeAsync(order, operation, ticket, plan, result.ProviderReference, isReplay, cancellationToken);

            var status = result.Outcome == ProviderOperationOutcome.Rejected
                ? ServicingOperationStatus.NeedsReconciliation
                : ServicingOperationStatus.AwaitingExternal;

            return await SettleAsync(
                order, operation, ticket, plan,
                status,
                status == ServicingOperationStatus.NeedsReconciliation
                    ? CommandReceiptStatus.NeedsReconciliation
                    : CommandReceiptStatus.Unknown,
                ProviderOperationOutcome.Confirmed,
                result.Outcome,
                releaseClaim: false,
                result.Outcome == ProviderOperationOutcome.Rejected
                    ? ChangeDocumentOutcome.Rejected
                    : ChangeDocumentOutcome.Pending,
                isReplay,
                cancellationToken);
        }

        private async Task<VoluntaryChangeOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedChangePlan plan,
            string? providerReference,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var staged = order.PrepareVoluntaryChange(ToArgs(plan), _idGenerator, _clock);

            ticket.Revalidate(
                operation.OperationId,
                plan.TicketCouponId,
                plan.ReplacementOrderServiceId,
                plan.QuotedChangeId,
                plan.TargetSelectionRef,
                providerReference,
                _callerContext.ActorId,
                CallerScope.For(_callerContext),
                _idGenerator,
                _clock);

            var changed = order.CommitVoluntaryChange(staged, _clock);

            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Completed,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Completed, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            await _projector.ProjectAsync(order.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(
                order, operation, ticket, plan, changed,
                ProviderOperationOutcome.Confirmed, ProviderOperationOutcome.Confirmed,
                ServicingOperationStatus.Completed, ChangeDocumentOutcome.Revalidated, isReplay);
        }

        private async Task<VoluntaryChangeOutcome> DeferToExchangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ChangeMonetaryOutcome monetaryOutcome,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Rejected,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Rejected, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(
                order, operation, ticket, null, null,
                ProviderOperationOutcome.Rejected, ProviderOperationOutcome.Pending,
                ServicingOperationStatus.Rejected, ChangeDocumentOutcome.NotAttempted, false)
                with
                { MonetaryOutcome = monetaryOutcome, DeferredToExchange = true };
        }

        private async Task<VoluntaryChangeOutcome> SettleEligibilityAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentChangeEligibility eligibility,
            CancellationToken cancellationToken)
        {
            var status = eligibility.Outcome == DocumentChangeEligibilityOutcome.PendingEvidence
                ? ServicingOperationStatus.AwaitingExternal
                : ServicingOperationStatus.Rejected;

            await _operationStore.TransitionAsync(
                operation.OperationId, status, operation.ClaimGeneration, cancellationToken);

            await _receipts.SetStatusAsync(
                operation.ReceiptId,
                status == ServicingOperationStatus.AwaitingExternal
                    ? CommandReceiptStatus.Pending
                    : CommandReceiptStatus.Rejected,
                cancellationToken);

            if (status == ServicingOperationStatus.Rejected)
                await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(
                order, operation, ticket, null, null,
                ProviderOperationOutcome.Rejected, ProviderOperationOutcome.Pending,
                status,
                eligibility.Outcome switch
                {
                    DocumentChangeEligibilityOutcome.ReissueRequired => ChangeDocumentOutcome.ReissueRequired,
                    DocumentChangeEligibilityOutcome.PendingEvidence => ChangeDocumentOutcome.Pending,
                    _ => ChangeDocumentOutcome.Denied
                },
                false);
        }

        private async Task<VoluntaryChangeOutcome> SettleAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedChangePlan? plan,
            ServicingOperationStatus operationStatus,
            CommandReceiptStatus receiptStatus,
            ProviderOperationOutcome reservationOutcome,
            ProviderOperationOutcome revalidationOutcome,
            bool releaseClaim,
            ChangeDocumentOutcome documentOutcome,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId, operationStatus, operation.ClaimGeneration, cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, receiptStatus, cancellationToken);

            if (releaseClaim)
                await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(
                order, operation, ticket, plan, null,
                reservationOutcome, revalidationOutcome, operationStatus, documentOutcome, isReplay);
        }

        private async Task<VoluntaryChangeOutcome?> ReplayUnfinishedAsync(
            Order order,
            OrderOperation operation,
            AcceptedChangePlan plan,
            CancellationToken cancellationToken)
        {
            var prior = await _operationStore.FindAsync(operation.OperationId, cancellationToken);

            if (prior is null)
                return null;

            if (prior.Status is not (ServicingOperationStatus.Executing
                or ServicingOperationStatus.AwaitingExternal
                or ServicingOperationStatus.NeedsReconciliation))
                return null;

            var tickets = await _tickets.ListByOrderAsync(order.Id, cancellationToken);
            var ticket = tickets.Single(candidate => candidate.Id == plan.ElectronicTicketId);

            if (plan.ReservationOutcome == ProviderOperationOutcome.Confirmed)
                return await RevalidateAsync(order, operation, ticket, plan, true, true, cancellationToken);

            var recovered = await _reservations.RecoverAsync(
                new ReservationChangeRecoveryRequest(
                    ReservationKey(operation, plan), order.Id, operation.OperationId),
                cancellationToken);

            return await AfterReservationAsync(order, operation, ticket, plan, recovered, true, cancellationToken);
        }

        private async Task<VoluntaryChangeOutcome> ReplayFinalizedAsync(
            Order order,
            OrderOperation operation,
            Entities.OrderChange committed,
            CancellationToken cancellationToken)
        {
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var plan = await _plans.FindAsync(operation.OperationId, cancellationToken);
            var tickets = await _tickets.ListByOrderAsync(order.Id, cancellationToken);
            var ticket = tickets.Single(candidate => candidate.Id == plan!.ElectronicTicketId);

            var changed = new ChangedServices(
                committed.Id,
                plan!.ReplacedOrderServiceId,
                plan.ReplacementOrderServiceId,
                plan.ReplacementOrderSegmentId,
                plan.ElectronicTicketId,
                plan.TicketCouponId);

            return Outcome(
                order, operation, ticket, plan, changed,
                ProviderOperationOutcome.Confirmed, ProviderOperationOutcome.Confirmed,
                ServicingOperationStatus.Completed, ChangeDocumentOutcome.Revalidated, true);
        }

        private static AcceptedVoluntaryChangeArgs ToArgs(AcceptedChangePlan plan)
            => new(
                plan.Accepted,
                plan.ReplacementOrderServiceId,
                plan.ReplacementOrderSegmentId,
                plan.OperationId);

        private static Entities.OrderChange? CommittedChange(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.VoluntaryChange);

        private async Task<(ElectronicTicket Ticket, Domain.ElectronicTicketAggregate.Entities.TicketCoupon Coupon)>
            ResolveDocumentAsync(long orderId, long orderServiceId, CancellationToken cancellationToken)
        {
            var tickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);

            foreach (var ticket in tickets)
            {
                var coupon = ticket.Coupons.FirstOrDefault(candidate =>
                    candidate.CurrentOrderServiceId == orderServiceId);

                if (coupon is not null)
                    return (ticket, coupon);
            }

            throw ExceptionFactory.AccountableDocumentNotFound(orderServiceId, orderId);
        }

        private string ReservationKey(OrderOperation operation, AcceptedChangePlan plan)
            => _operations.ProviderOperationKey(operation, $"{ReservationStep}:{plan.ReplacedOrderServiceId}");

        private string RevalidationKey(OrderOperation operation, ElectronicTicket ticket)
            => _operations.ProviderOperationKey(operation, $"{RevalidationStep}:{ticket.Id}");

        private async Task TryReleaseRejectedAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken)
        {
            try
            {
                await _operations.ResolveAsync(orderId, operation, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
            }
        }

        private static VoluntaryChangeOutcome Outcome(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedChangePlan? plan,
            ChangedServices? changed,
            ProviderOperationOutcome reservationOutcome,
            ProviderOperationOutcome revalidationOutcome,
            ServicingOperationStatus operationStatus,
            ChangeDocumentOutcome documentOutcome,
            bool isReplay)
            => new(
                order.Id,
                operation.OperationId,
                OrderChangeType.VoluntaryChange,
                ServicingOperationKind.Revalidate,
                ticket.Id,
                ticket.DocumentNumber,
                ticket.DocumentVersion,
                plan?.TicketCouponId,
                changed?.OrderChangeId,
                plan?.ReplacedOrderServiceId,
                plan?.ReplacementOrderServiceId,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                reservationOutcome,
                revalidationOutcome,
                documentOutcome,
                operationStatus,
                ChangeMonetaryOutcome.Even,
                false,
                isReplay);
    }
}
