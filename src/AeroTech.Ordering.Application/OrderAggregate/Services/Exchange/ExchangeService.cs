using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain.Ports.ReservationChange;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed class ExchangeService : IExchangeService
    {
        public const string QuoteStep = "exchange-quote";
        public const string EligibilityStep = "document-exchange-eligibility";
        public const string ReservationStep = "exchange-reservation";
        public const string DocumentExchangeStep = "document-exchange";

        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly ExchangePreconditions _preconditions;
        private readonly IExchangeQuotePort _quotes;
        private readonly IReservationChangePort _reservations;
        private readonly IDocumentExchangePort _documents;
        private readonly IAcceptedExchangePlanStore _plans;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly ICallerContext _callerContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public ExchangeService(
            IOrderRepository orders,
            IElectronicTicketRepository tickets,
            ExchangePreconditions preconditions,
            IExchangeQuotePort quotes,
            IReservationChangePort reservations,
            IDocumentExchangePort documents,
            IAcceptedExchangePlanStore plans,
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
            _preconditions = preconditions;
            _quotes = quotes;
            _reservations = reservations;
            _documents = documents;
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

        public async Task<ExchangeQuoteOutcome> QuoteAsync(
            long orderId,
            long predecessorOrderServiceId,
            CancellationToken cancellationToken = default)
        {
            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var scope = await _preconditions.EnsureAsync(order, predecessorOrderServiceId, cancellationToken);

            var quote = await _quotes.QuoteAsync(
                new ExchangeQuoteRequest(
                    order.Id,
                    order.CommercialVersion,
                    scope.PredecessorTicket.Id,
                    scope.PredecessorService.Id,
                    scope.PredecessorCoupon.Id,
                    order.CurrencyId),
                cancellationToken);

            ExchangeQuoteBinding.EnsureQuoteBindsToTheOrder(
                quote, order, scope.PredecessorTicket, scope.PredecessorService.Id, scope.PredecessorCoupon.Id);

            return new ExchangeQuoteOutcome(
                order.Id,
                order.CommercialVersion,
                scope.PredecessorTicket.Id,
                scope.PredecessorTicket.DocumentNumber,
                scope.PredecessorService.Id,
                scope.PredecessorCoupon.Id,
                quote.QuotedExchangeId,
                quote.SourceSystem,
                quote.TargetSelectionRef,
                quote.PricingSource,
                quote.MonetaryOutcome,
                quote.SaleCurrencyId,
                quote.ExpiresAt,
                quote.ContinuedOrderServiceIds,
                quote.Replacement,
                quote.PricingLines,
                quote.SuccessorCoupon,
                quote.SourcePricingReference);
        }

        public async Task<ExchangeOutcome> ExchangeAsync(
            ExchangeExecution execution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            ArgumentException.ThrowIfNullOrWhiteSpace(execution.IdempotencyKey);

            if (string.IsNullOrWhiteSpace(execution.QuotedExchangeId))
                throw ExceptionFactory.OrderExchangeRequiresQuote(execution.OrderId);

            if (execution.ExpectedCommercialVersion is null)
                throw ExceptionFactory.ExpectedCommercialVersionRequired(execution.OrderId);

            var order = await _orders.GetAsync(execution.OrderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(execution.OrderId);

            var operation = await _operations.BeginAsync(
                execution.OrderId,
                ServicingOperationKind.Exchange,
                execution.IdempotencyKey,
                new
                {
                    Operation = "Exchange",
                    OrderId = execution.OrderId,
                    OrderServiceId = execution.PredecessorOrderServiceId,
                    QuotedExchangeId = execution.QuotedExchangeId,
                    ExpectedCommercialVersion = execution.ExpectedCommercialVersion
                },
                execution.ExpectedCommercialVersion,
                cancellationToken);

            var committed = CommittedExchange(order, operation.OperationId);

            if (committed is not null)
                return await ReplayCompletedAsync(order, operation, committed, cancellationToken);

            var plan = await _plans.FindAsync(operation.OperationId, cancellationToken);

            if (plan is not null)
                return await ResumeAsync(order, operation, plan, cancellationToken);

            return await ExecuteFreshAsync(order, operation, execution, cancellationToken);
        }

        private async Task<ExchangeOutcome> ExecuteFreshAsync(
            Order order,
            OrderOperation operation,
            ExchangeExecution execution,
            CancellationToken cancellationToken)
        {
            var expectedCommercialVersion = execution.ExpectedCommercialVersion!.Value;
            ExchangeScope scope;

            try
            {
                if (expectedCommercialVersion != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(
                        expectedCommercialVersion, order.Id, order.CommercialVersion);

                scope = await _preconditions.EnsureAsync(
                    order, execution.PredecessorOrderServiceId, cancellationToken);

                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.Executing,
                    operation.ClaimGeneration,
                    cancellationToken);
            }
            catch
            {
                await TryRejectAsync(order.Id, operation, cancellationToken);
                throw;
            }

            AcceptedExchange accepted;

            try
            {
                accepted = await _quotes.AcceptQuotedExchangeAsync(
                    new AcceptedQuotedExchangeSelection(
                        _operations.ProviderOperationKey(operation, QuoteStep),
                        order.Id,
                        operation.OperationId,
                        execution.QuotedExchangeId,
                        expectedCommercialVersion,
                        scope.PredecessorTicket.Id,
                        scope.PredecessorService.Id,
                        scope.PredecessorCoupon.Id,
                        order.CurrencyId),
                    cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var plan = NewPlan(order, operation, scope, execution.QuotedExchangeId, expectedCommercialVersion, accepted);
            string? deferralReason;

            try
            {
                ExchangeQuoteBinding.EnsureAcceptedBindsToTheRequest(
                    accepted,
                    order,
                    scope.PredecessorTicket,
                    scope.PredecessorService.Id,
                    scope.PredecessorCoupon.Id,
                    execution.QuotedExchangeId,
                    expectedCommercialVersion,
                    _clock.GetDateTime());

                deferralReason = ExchangePricingPolicy.DeferralReason(accepted);

                if (deferralReason is null)
                    order.PrepareExchange(ToArgs(plan, scope.PredecessorTicket), _idGenerator, _clock);
            }
            catch (BusinessException rejection)
            {
                await TryRecordRejectionAsync(order.Id, operation, plan, rejection, cancellationToken);
                throw;
            }

            if (deferralReason is not null)
                return await DeferAsync(
                    order,
                    operation,
                    scope.PredecessorTicket,
                    plan with
                    {
                        Disposition = AcceptedExchangeDisposition.DeferredToExpandedExchange,
                        DispositionDetail = deferralReason
                    },
                    false,
                    cancellationToken);

            try
            {
                await _plans.SaveAsync(plan, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            return await AdvanceAsync(order, operation, scope.PredecessorTicket, plan, false, cancellationToken);
        }

        private async Task<ExchangeOutcome> ResumeAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            CancellationToken cancellationToken)
        {
            var predecessor = await _tickets.GetAsync(plan.PredecessorElectronicTicketId, cancellationToken)
                              ?? throw ExceptionFactory.AccountableDocumentNotFound(
                                  plan.PredecessorElectronicTicketId, order.Id);

            return plan.Disposition switch
            {
                AcceptedExchangeDisposition.DeferredToExpandedExchange
                    => await DeferAsync(order, operation, predecessor, plan, true, cancellationToken),
                AcceptedExchangeDisposition.Rejected
                    => await ReplayRejectionAsync(order, operation, plan, cancellationToken),
                _ => await AdvanceAsync(order, operation, predecessor, plan, true, cancellationToken)
            };
        }

        private async Task<ExchangeOutcome> AdvanceAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.IsEligibilityTerminal)
                return await SettleTerminalAsync(
                    order, operation, predecessor, plan, ExchangeDocumentOutcome.Denied, isReplay, cancellationToken);

            if (plan.IsReservationRejected)
                return await SettleTerminalAsync(
                    order, operation, predecessor, plan, ExchangeDocumentOutcome.NotAttempted, isReplay,
                    cancellationToken);

            if (await AwaitsReconciliationAsync(operation, cancellationToken)
                || order.CommercialVersion != plan.ExpectedCommercialVersion
                || plan.IsDocumentExchangeRejected)
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            if (plan.IsDocumentExchangeConfirmed)
                return await FinalizeAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            if (plan.IsEligibilityEstablished)
                return await EnterReservationAsync(
                    order, operation, predecessor, plan, dispatchFresh: false, isReplay, cancellationToken);

            DocumentExchangeEligibility eligibility;

            try
            {
                eligibility = await _documents.CheckEligibilityAsync(
                    new DocumentExchangeEligibilityRequest(
                        EligibilityKey(operation, plan),
                        order.Id,
                        operation.OperationId,
                        plan.PredecessorDocumentNumber,
                        plan.PredecessorCouponNumber,
                        plan.TargetSelectionRef,
                        plan.SourcePricingReference),
                    cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            await _plans.RecordEligibilityOutcomeAsync(
                operation.OperationId, eligibility.Outcome, eligibility.Detail, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var evaluated = plan with { EligibilityOutcome = eligibility.Outcome, EligibilityDetail = eligibility.Detail };

            return eligibility.Outcome switch
            {
                DocumentExchangeEligibilityOutcome.Eligible => await EnterReservationAsync(
                    order, operation, predecessor, evaluated, dispatchFresh: true, isReplay, cancellationToken),
                DocumentExchangeEligibilityOutcome.Denied => await SettleTerminalAsync(
                    order, operation, predecessor, evaluated, ExchangeDocumentOutcome.Denied, isReplay, cancellationToken),
                _ => await SettleAsync(
                    order, operation, predecessor, evaluated,
                    ServicingOperationStatus.AwaitingExternal,
                    CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Pending,
                    isReplay,
                    cancellationToken)
            };
        }

        private async Task<ExchangeOutcome> EnterReservationAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            bool dispatchFresh,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.IsReservationConfirmed)
                return await EnterDocumentExchangeAsync(
                    order, operation, predecessor, plan, dispatchFresh: false, isReplay, cancellationToken);

            if (dispatchFresh)
                return await ApplyReservationChangeAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            ReservationChangeRecovery recovered;

            try
            {
                recovered = await _reservations.RecoverAsync(
                    new ReservationChangeRecoveryRequest(ReservationKey(operation, plan), order.Id, operation.OperationId),
                    cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            if (!recovered.WasDispatched)
                return await ApplyReservationChangeAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            return await AfterReservationAsync(
                order, operation, predecessor, plan, recovered.AsResult(), fromFreshApply: false, isReplay,
                cancellationToken);
        }

        private async Task<ExchangeOutcome> ApplyReservationChangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
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
                        plan.PredecessorOrderServiceId,
                        plan.ReplacementOrderServiceId,
                        plan.ReplacementOrderSegmentId,
                        plan.Accepted.Replacement.Segment.CapacityReference,
                        plan.Accepted.Replacement.Segment.BookingClass,
                        plan.Accepted.Replacement.BeneficiaryTravellerIds[0]),
                    cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            return await AfterReservationAsync(
                order, operation, predecessor, plan, result, fromFreshApply: true, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> AfterReservationAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ReservationChangeResult result,
            bool fromFreshApply,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            await _plans.RecordReservationOutcomeAsync(
                operation.OperationId, result.Outcome, result.ExternalReservationRef, cancellationToken);

            var recorded = plan with
            {
                ReservationOutcome = result.Outcome,
                ReservationExternalRef = result.ExternalReservationRef ?? plan.ReservationExternalRef
            };

            if (result.Outcome == ProviderOperationOutcome.Rejected)
                return await SettleTerminalAsync(
                    order, operation, predecessor, recorded, ExchangeDocumentOutcome.NotAttempted, isReplay,
                    cancellationToken);

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return await SettleAsync(
                    order, operation, predecessor, recorded,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.NotAttempted,
                    isReplay,
                    cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await EnterDocumentExchangeAsync(
                order, operation, predecessor, recorded, fromFreshApply, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> EnterDocumentExchangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            bool dispatchFresh,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.IsDocumentExchangeConfirmed)
                return await FinalizeAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            DocumentExchangeResult result;

            try
            {
                if (dispatchFresh)
                {
                    result = await DispatchDocumentExchangeAsync(order, operation, plan, cancellationToken);
                }
                else
                {
                    var recovered = await _documents.RecoverAsync(
                        new DocumentExchangeRecoveryRequest(
                            DocumentExchangeKey(operation, plan),
                            order.Id,
                            operation.OperationId,
                            plan.PredecessorDocumentNumber),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await DispatchDocumentExchangeAsync(order, operation, plan, cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            return await AfterDocumentExchangeAsync(order, operation, predecessor, plan, result, isReplay, cancellationToken);
        }

        private async Task<DocumentExchangeResult> DispatchDocumentExchangeAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            CancellationToken cancellationToken)
            => await _documents.ExchangeAsync(
                new DocumentExchangeRequest(
                    DocumentExchangeKey(operation, plan),
                    order.Id,
                    operation.OperationId,
                    plan.PredecessorDocumentNumber,
                    plan.PredecessorCouponNumber,
                    plan.QuotedExchangeId,
                    plan.TargetSelectionRef,
                    plan.SourcePricingReference,
                    plan.ReplacementOrderServiceId,
                    plan.ReplacementOrderSegmentId,
                    plan.Accepted.Replacement.Segment.FlightNumber,
                    plan.Accepted.Replacement.Segment.DepartureAt),
                cancellationToken);

        private async Task<ExchangeOutcome> AfterDocumentExchangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            DocumentExchangeResult result,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (ContradictsDurableEvidence(plan, result))
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            await _plans.RecordDocumentExchangeOutcomeAsync(
                operation.OperationId, result.Outcome, result.ProviderReference, result.Successor, result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var recorded = plan with
            {
                DocumentExchangeOutcome = result.Outcome,
                DocumentExchangeProviderReference = result.ProviderReference ?? plan.DocumentExchangeProviderReference,
                DocumentExchangeDetail = result.Detail ?? plan.DocumentExchangeDetail,
                Successor = result.Successor ?? plan.Successor
            };

            return result.Outcome switch
            {
                ProviderOperationOutcome.Confirmed
                    => await FinalizeAsync(order, operation, predecessor, recorded, isReplay, cancellationToken),
                ProviderOperationOutcome.Rejected
                    => await ReconcileAsync(order, operation, predecessor, recorded, isReplay, cancellationToken),
                _ => await SettleAsync(
                    order, operation, predecessor, recorded,
                    ServicingOperationStatus.AwaitingExternal,
                    CommandReceiptStatus.Unknown,
                    ExchangeDocumentOutcome.Pending,
                    isReplay,
                    cancellationToken)
            };
        }

        private async Task<ExchangeOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.Successor is not { } successor
                || !await IsUsableSuccessorIdentityAsync(predecessor, successor, cancellationToken))
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            var staged = order.PrepareExchange(ToArgs(plan, predecessor), _idGenerator, _clock);

            predecessor.MarkExchanged(
                new ExchangeProvenance(
                    operation.OperationId,
                    plan.QuotedExchangeId,
                    plan.TargetSelectionRef,
                    plan.SourcePricingReference,
                    plan.DocumentExchangeProviderReference,
                    _callerContext.ActorId,
                    CallerScope.For(_callerContext)),
                plan.PredecessorTicketCouponId,
                plan.SuccessorElectronicTicketId,
                successor.DocumentNumber,
                plan.SuccessorTicketCouponId,
                successor.CouponNumber,
                plan.ReplacementOrderServiceId,
                _idGenerator,
                _clock);

            var exchanged = order.CommitExchange(staged, _idGenerator, _clock);

            var successorTicket = ElectronicTicket.IssueSuccessor(
                SuccessorIssuance(order, predecessor, plan, successor, exchanged), _idGenerator, _clock);

            await _tickets.AddAsync(successorTicket, cancellationToken);

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
                order, operation, predecessor, plan, successorTicket,
                exchanged.OrderChangeId, exchanged.PriceChangeSetId,
                ServicingOperationStatus.Completed, ExchangeDocumentOutcome.Exchanged, isReplay);
        }

        private static SuccessorTicketIssuance SuccessorIssuance(
            Order order,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            ExchangedOrder exchanged)
        {
            var segment = plan.Accepted.Replacement.Segment;
            var coupon = plan.Accepted.SuccessorCoupon;

            return new SuccessorTicketIssuance(
                plan.SuccessorElectronicTicketId,
                plan.SuccessorTicketCouponId,
                predecessor.Id,
                plan.PredecessorTicketCouponId,
                plan.OperationId,
                order.Id,
                predecessor.TravelerId,
                successor.DocumentNumber,
                successor.CouponNumber,
                successor.IssuerCarrierId,
                successor.IssuingOfficeId,
                successor.Authority,
                successor.VoidDeadline,
                plan.SaleCurrencyId,
                plan.ReplacementOrderServiceId,
                plan.ReplacementOrderSegmentId,
                new IssuedSegmentSnapshot(
                    segment.MarketingAirlineId,
                    segment.FlightNumber,
                    segment.OriginAirportId,
                    segment.DestinationAirportId,
                    segment.DepartureAt,
                    segment.ArrivalAt,
                    segment.BookingClass),
                coupon.FareBasis,
                coupon.IssuanceValue,
                coupon.PriceLinks
                    .Select(link => new TicketCouponPriceLink(
                        exchanged.PricingLineIdsBySourceRef[link.SourceLineRef],
                        null,
                        link.AttributedValue))
                    .ToList());
        }

        private async Task<bool> IsUsableSuccessorIdentityAsync(
            ElectronicTicket predecessor,
            SuccessorDocumentIdentity successor,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(successor.DocumentNumber) || successor.CouponNumber < 1)
                return false;

            if (string.Equals(successor.DocumentNumber, predecessor.DocumentNumber, StringComparison.Ordinal))
                return false;

            return await _tickets.FindByDocumentNumberAsync(successor.DocumentNumber, cancellationToken) is null;
        }

        private static bool ContradictsDurableEvidence(AcceptedExchangePlan plan, DocumentExchangeResult result)
        {
            if (plan.Successor is { } known
                && result.Successor is { } reported
                && (!string.Equals(known.DocumentNumber, reported.DocumentNumber, StringComparison.Ordinal)
                    || known.CouponNumber != reported.CouponNumber))
                return true;

            return plan.DocumentExchangeProviderReference is { } knownReference
                   && result.ProviderReference is { } reportedReference
                   && !string.Equals(knownReference, reportedReference, StringComparison.Ordinal);
        }

        private async Task<bool> AwaitsReconciliationAsync(OrderOperation operation, CancellationToken cancellationToken)
            => (await _operationStore.FindAsync(operation.OperationId, cancellationToken))?.Status
               == ServicingOperationStatus.NeedsReconciliation;

        private async Task<ExchangeOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            bool isReplay,
            CancellationToken cancellationToken)
            => await SettleAsync(
                order, operation, predecessor, plan,
                ServicingOperationStatus.NeedsReconciliation,
                CommandReceiptStatus.NeedsReconciliation,
                plan.IsDocumentExchangeRejected ? ExchangeDocumentOutcome.Rejected : ExchangeDocumentOutcome.Pending,
                isReplay,
                cancellationToken);

        private async Task<ExchangeOutcome> SettleAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ServicingOperationStatus operationStatus,
            CommandReceiptStatus receiptStatus,
            ExchangeDocumentOutcome documentOutcome,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId, operationStatus, operation.ClaimGeneration, cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, receiptStatus, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, predecessor, plan, null, null, null, operationStatus, documentOutcome, isReplay);
        }

        private async Task<ExchangeOutcome> SettleTerminalAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ExchangeDocumentOutcome documentOutcome,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            await RejectAsync(order.Id, operation, cancellationToken);

            return Outcome(
                order, operation, predecessor, plan, null, null, null,
                ServicingOperationStatus.Rejected, documentOutcome, isReplay);
        }

        private async Task<ExchangeOutcome> DeferAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            await _plans.SaveAsync(plan, cancellationToken);
            await RejectAsync(order.Id, operation, cancellationToken);

            return Outcome(
                order, operation, predecessor, plan, null, null, null,
                ServicingOperationStatus.Rejected, ExchangeDocumentOutcome.NotAttempted, isReplay);
        }

        private async Task<ExchangeOutcome> ReplayRejectionAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            CancellationToken cancellationToken)
        {
            await RejectAsync(order.Id, operation, cancellationToken);

            throw ExceptionFactory.ExchangeRejectionReplayed(
                plan.RejectionCode ?? 0,
                plan.RejectionHttpStatus ?? 422,
                plan.DispositionDetail ?? string.Empty);
        }

        private async Task RejectAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Rejected,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Rejected, cancellationToken);
            await _operations.ResolveAsync(orderId, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task TryRejectAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken)
        {
            try
            {
                await RejectAsync(orderId, operation, cancellationToken);
            }
            catch (Exception)
            {
            }
        }

        private async Task TryRecordRejectionAsync(
            long orderId,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            BusinessException rejection,
            CancellationToken cancellationToken)
        {
            try
            {
                await _plans.SaveAsync(
                    plan with
                    {
                        Disposition = AcceptedExchangeDisposition.Rejected,
                        DispositionDetail = rejection.Message,
                        RejectionCode = rejection.Code,
                        RejectionHttpStatus = rejection.HttpStatus
                    },
                    cancellationToken);

                await RejectAsync(orderId, operation, cancellationToken);
            }
            catch (Exception)
            {
            }
        }

        private async Task MarkAwaitingExternalAsync(OrderOperation operation)
        {
            try
            {
                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.AwaitingExternal,
                    operation.ClaimGeneration,
                    CancellationToken.None);

                await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Unknown, CancellationToken.None);
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception)
            {
            }
        }

        private async Task<ExchangeOutcome> ReplayCompletedAsync(
            Order order,
            OrderOperation operation,
            Entities.OrderChange committed,
            CancellationToken cancellationToken)
        {
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var plan = await _plans.FindAsync(operation.OperationId, cancellationToken)
                       ?? throw ExceptionFactory.AcceptedExchangePlanNotFound(operation.OperationId);

            var predecessor = await _tickets.GetAsync(plan.PredecessorElectronicTicketId, cancellationToken)
                              ?? throw ExceptionFactory.AccountableDocumentNotFound(
                                  plan.PredecessorElectronicTicketId, order.Id);

            var successor = await _tickets.GetAsync(plan.SuccessorElectronicTicketId, cancellationToken);

            var changeSet = order.PriceChangeSets.Single(set => set.ChangeId == committed.Id);

            return Outcome(
                order, operation, predecessor, plan, successor, committed.Id, changeSet.Id,
                ServicingOperationStatus.Completed, ExchangeDocumentOutcome.Exchanged, true);
        }

        private AcceptedExchangePlan NewPlan(
            Order order,
            OrderOperation operation,
            ExchangeScope scope,
            string quotedExchangeId,
            int expectedCommercialVersion,
            AcceptedExchange accepted)
            => new(
                operation.OperationId,
                order.Id,
                quotedExchangeId,
                accepted.SourceSystem ?? string.Empty,
                accepted.TargetSelectionRef ?? string.Empty,
                accepted.SourcePricingReference,
                accepted.PricingSource,
                accepted.SaleCurrencyId,
                scope.PredecessorTicket.Id,
                scope.PredecessorTicket.DocumentNumber,
                scope.PredecessorCoupon.Id,
                scope.PredecessorCoupon.CouponNumber,
                scope.PredecessorService.Id,
                _idGenerator.NewId(),
                _idGenerator.NewId(),
                _idGenerator.NewId(),
                _idGenerator.NewId(),
                expectedCommercialVersion,
                accepted.MonetaryOutcome,
                accepted);

        private AcceptedExchangeArgs ToArgs(AcceptedExchangePlan plan, ElectronicTicket predecessor)
            => new(
                plan.Accepted,
                plan.ReplacementOrderServiceId,
                plan.ReplacementOrderSegmentId,
                plan.SuccessorElectronicTicketId,
                plan.SuccessorTicketCouponId,
                predecessor.CarriedPricingLineIds(),
                plan.OperationId,
                _callerContext.ActorId,
                CallerScope.For(_callerContext));

        private static Entities.OrderChange? CommittedExchange(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.Exchange);

        private string EligibilityKey(OrderOperation operation, AcceptedExchangePlan plan)
            => _operations.ProviderOperationKey(operation, $"{EligibilityStep}:{plan.PredecessorElectronicTicketId}");

        private string ReservationKey(OrderOperation operation, AcceptedExchangePlan plan)
            => _operations.ProviderOperationKey(operation, $"{ReservationStep}:{plan.PredecessorOrderServiceId}");

        private string DocumentExchangeKey(OrderOperation operation, AcceptedExchangePlan plan)
            => _operations.ProviderOperationKey(operation, $"{DocumentExchangeStep}:{plan.PredecessorElectronicTicketId}");

        private static ExchangeOutcome Outcome(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ElectronicTicket? successor,
            long? orderChangeId,
            long? priceChangeSetId,
            ServicingOperationStatus operationStatus,
            ExchangeDocumentOutcome documentOutcome,
            bool isReplay)
            => new(
                order.Id,
                operation.OperationId,
                OrderChangeType.Exchange,
                ServicingOperationKind.Exchange,
                predecessor.Id,
                predecessor.DocumentNumber,
                predecessor.DocumentVersion,
                plan.PredecessorTicketCouponId,
                successor?.Id,
                successor?.DocumentNumber ?? plan.Successor?.DocumentNumber,
                successor?.DocumentVersion,
                successor is null ? null : plan.SuccessorTicketCouponId,
                plan.PredecessorOrderServiceId,
                orderChangeId is null ? null : plan.ReplacementOrderServiceId,
                orderChangeId,
                priceChangeSetId,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                plan.EligibilityOutcome,
                plan.ReservationOutcome,
                plan.DocumentExchangeOutcome ?? ProviderOperationOutcome.Pending,
                plan.DocumentExchangeProviderReference,
                documentOutcome,
                operationStatus,
                plan.MonetaryOutcome,
                plan.Disposition == AcceptedExchangeDisposition.DeferredToExpandedExchange,
                plan.Disposition == AcceptedExchangeDisposition.DeferredToExpandedExchange
                    ? plan.DispositionDetail
                    : null,
                isReplay);
    }
}
