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
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.DocumentRefund;
using AeroTech.Ordering.Domain.Ports.EmdExchange;
using AeroTech.Ordering.Domain.Ports.EmdAssociation;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;
using AeroTech.Ordering.Domain.Ports.ExchangeResidual;
using AeroTech.Ordering.Domain.Ports.RefundValue;
using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain.Ports.ReservationChange;
using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Contracts;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Domain._Shared.Resources;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed partial class ExchangeService : IExchangeService
    {
        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly ExchangePreconditions _preconditions;
        private readonly IExchangeQuotePort _quotes;
        private readonly IReservationChangePort _reservations;
        private readonly IDocumentExchangePort _documents;
        private readonly IExchangeFundingPort _funding;
        private readonly IRefundValuePort _refundValues;
        private readonly IExchangeResidualValuePort _residuals;
        private readonly IDocumentRefundPort _documentRefunds;
        private readonly IAncillaryExchangeDispositionPort _ancillaryDispositions;
        private readonly IEmdExchangePort _emdExchanges;
        private readonly IEmdAssociationPort _emdAssociations;
        private readonly IElectronicMiscDocumentRepository _miscDocuments;
        private readonly IAcceptedExchangePlanStore _plans;
        private readonly IOrderOperationCoordinator _operations;
        private readonly ExchangeOperationKeys _keys;
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
            IExchangeFundingPort funding,
            IRefundValuePort refundValues,
            IExchangeResidualValuePort residuals,
            IDocumentRefundPort documentRefunds,
            IAncillaryExchangeDispositionPort ancillaryDispositions,
            IEmdExchangePort emdExchanges,
            IEmdAssociationPort emdAssociations,
            IElectronicMiscDocumentRepository miscDocuments,
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
            _funding = funding;
            _refundValues = refundValues;
            _residuals = residuals;
            _documentRefunds = documentRefunds;
            _emdExchanges = emdExchanges;
            _ancillaryDispositions = ancillaryDispositions;
            _emdAssociations = emdAssociations;
            _miscDocuments = miscDocuments;
            _plans = plans;
            _operations = operations;
            _keys = new ExchangeOperationKeys(operations);
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
            IReadOnlyList<long> changedOrderServiceIds,
            CancellationToken cancellationToken = default)
        {
            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var scope = await _preconditions.EnsureAsync(order, changedOrderServiceIds, cancellationToken);

            var quote = await _quotes.QuoteAsync(QuoteRequest(order, scope), cancellationToken);

            ExchangeQuoteBinding.EnsureQuoteBindsToTheOrder(quote, order, scope);

            return new ExchangeQuoteOutcome(
                order.Id,
                order.CommercialVersion,
                scope.PredecessorTicket.Id,
                scope.PredecessorTicket.DocumentNumber,
                scope.ChangedOrderServiceIds,
                quote.QuotedExchangeId,
                quote.SourceSystem,
                quote.TargetSelectionRef,
                quote.PricingSource,
                quote.MonetaryOutcome,
                quote.SaleCurrencyId,
                quote.ExpiresAt,
                quote.Coupons,
                quote.PricingLines,
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

            var changed = ExchangePreconditions.NormalizeChangedServices(order, execution.ChangedOrderServiceIds);

            var operation = await _operations.BeginAsync(
                execution.OrderId,
                ServicingOperationKind.Exchange,
                execution.IdempotencyKey,
                new
                {
                    Operation = "Exchange",
                    OrderId = execution.OrderId,
                    ChangedOrderServiceIds = changed,
                    QuotedExchangeId = execution.QuotedExchangeId,
                    ExpectedCommercialVersion = execution.ExpectedCommercialVersion
                },
                execution.ExpectedCommercialVersion,
                cancellationToken);

            var committed = CommittedExchange(order, operation.OperationId);

            if (committed is not null
                && await OperationStatusAsync(operation, cancellationToken) == ServicingOperationStatus.Completed)
                return await ReplayCompletedAsync(order, operation, committed, cancellationToken);

            var plan = await _plans.FindAsync(operation.OperationId, cancellationToken);

            if (plan is not null)
                return await ResumeAsync(order, operation, plan, cancellationToken);

            return await ExecuteFreshAsync(order, operation, execution, changed, cancellationToken);
        }

        private async Task<ExchangeOutcome> ExecuteFreshAsync(
            Order order,
            OrderOperation operation,
            ExchangeExecution execution,
            IReadOnlyList<long> changed,
            CancellationToken cancellationToken)
        {
            var expectedCommercialVersion = execution.ExpectedCommercialVersion!.Value;
            ExchangeScope scope;

            try
            {
                if (expectedCommercialVersion != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(
                        expectedCommercialVersion, order.Id, order.CommercialVersion);

                scope = await _preconditions.EnsureAsync(order, changed, cancellationToken);

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
                        _keys.Quote(operation),
                        order.Id,
                        operation.OperationId,
                        execution.QuotedExchangeId,
                        expectedCommercialVersion,
                        scope.PredecessorTicket.Id,
                        scope.ChangedOrderServiceIds,
                        order.CurrencyId),
                    cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var plan = NewPlan(
                order, operation, scope, execution.QuotedExchangeId, expectedCommercialVersion, accepted,
                execution.FundingMethodRef);
            string? deferralReason;

            try
            {
                ExchangeQuoteBinding.EnsureAcceptedBindsToTheRequest(
                    accepted, order, scope, execution.QuotedExchangeId, expectedCommercialVersion, _clock.GetDateTime());

                deferralReason = ExchangePricingPolicy.DeferralReason(accepted);
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

            var ancillaryRequest = scope.AffectedAncillaries.Count == 0
                ? null
                : ExchangeAncillaryPlanner.Request(
                    order.Id, operation.OperationId, execution.QuotedExchangeId, scope);
            AncillaryExchangeDispositionResult? ancillaryDecision;

            try
            {
                ancillaryDecision = ancillaryRequest is null
                    ? null
                    : await _ancillaryDispositions.DecideAsync(ancillaryRequest, cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            try
            {
                ExchangePricingPolicy.EnsureWellFormed(accepted);
                ExchangePricingPolicy.EnsureResidualFulfillmentIsCoherent(accepted, execution.QuotedExchangeId);

                if (plan.RequiresFunding && string.IsNullOrWhiteSpace(plan.FundingMethodRef))
                    throw ExceptionFactory.ExchangeFundingMethodRequired(
                        plan.QuotedExchangeId, plan.AddCollect!.Amount);

                if (ancillaryRequest is not null && ancillaryDecision is not null)
                {
                    var acceptedAncillaries = ExchangeAncillaryPlanner.Accept(
                        ancillaryRequest, scope, plan.Coupons, ancillaryDecision);

                    plan = plan with
                    {
                        AncillaryDispositions = acceptedAncillaries,
                        AncillaryExchangeGroups = ExchangeAncillaryPlanner.AcceptExchangeGroups(
                            scope, plan.Coupons, ancillaryDecision, acceptedAncillaries)
                    };

                    ExchangeAncillaryPlanner.EnsureExecutable(plan.Ancillaries);
                }

                order.PrepareExchange(ToArgs(plan, scope.PredecessorTicket), _idGenerator, _clock);
            }
            catch (BusinessException rejection)
            {
                await TryRecordRejectionAsync(order.Id, operation, plan, rejection, cancellationToken);
                throw;
            }

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

            if (plan.IsFundingGuaranteeRejected)
                return await SettleTerminalAsync(
                    order, operation, predecessor, plan, ExchangeDocumentOutcome.NotAttempted, isReplay,
                    cancellationToken);

            if (plan.IsReservationRejected)
                return await ReleaseThenSettleAsync(
                    order, operation, predecessor, plan, ExchangeFundingReleaseReason.ReservationRejected,
                    dispatchFresh: false, isReplay, cancellationToken);

            if (plan.IsDocumentExchangeRejected)
                return await ReleaseThenSettleAsync(
                    order, operation, predecessor, plan, ExchangeFundingReleaseReason.DocumentRejected,
                    dispatchFresh: false, isReplay, cancellationToken);

            var alreadyMaterialized = await AlreadyMaterializedAsync(order, operation, plan, cancellationToken);

            if (await AwaitsReconciliationAsync(operation, cancellationToken)
                || (alreadyMaterialized is null && order.CommercialVersion != plan.ExpectedCommercialVersion))
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, alreadyMaterialized);

            if (plan.IsDocumentExchangeConfirmed)
                return await FinalizeAsync(
                    order, operation, predecessor, plan, documentJustConfirmed: false, isReplay, cancellationToken);

            if (plan.IsEligibilityEstablished)
                return await EnterFundingAsync(
                    order, operation, predecessor, plan, dispatchFresh: false, isReplay, cancellationToken);

            DocumentExchangeEligibility eligibility;

            try
            {
                eligibility = await _documents.CheckEligibilityAsync(
                    new DocumentExchangeEligibilityRequest(
                        _keys.Eligibility(operation, plan),
                        order.Id,
                        operation.OperationId,
                        plan.PredecessorDocumentNumber,
                        plan.Coupons.Select(coupon => coupon.PredecessorCouponNumber).ToList(),
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
                DocumentExchangeEligibilityOutcome.Eligible => await EnterFundingAsync(
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
                    new ReservationChangeRecoveryRequest(_keys.Reservation(operation, plan), order.Id, operation.OperationId),
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
                        _keys.Reservation(operation, plan),
                        order.Id,
                        operation.OperationId,
                        null,
                        plan.ReplacedCoupons.Select(coupon => ReservationItem(plan, coupon)).ToList()),
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

        private static ReservationChangeItem ReservationItem(AcceptedExchangePlan plan, AcceptedExchangePlanCoupon coupon)
        {
            var replacement = AcceptedCoupon(plan, coupon).Replacement!;

            return new ReservationChangeItem(
                coupon.PredecessorOrderServiceId,
                coupon.ReplacementOrderServiceId!.Value,
                coupon.ReplacementOrderSegmentId!.Value,
                replacement.Segment.CapacityReference,
                replacement.Segment.BookingClass,
                plan.PredecessorTravellerId);
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
                return await ReleaseThenSettleAsync(
                    order, operation, predecessor, recorded, ExchangeFundingReleaseReason.ReservationRejected,
                    dispatchFresh: true, isReplay, cancellationToken);

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

        private async Task<ExchangeOutcome> EnterFundingAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            bool dispatchFresh,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (!plan.RequiresFunding || plan.IsFundingGuaranteed)
                return await EnterReservationAsync(
                    order, operation, predecessor, plan,
                    plan.RequiresFunding ? false : dispatchFresh,
                    isReplay, cancellationToken);

            if (!plan.CanReproduceFundingRequest)
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            ExchangeFundingResult result;

            try
            {
                if (dispatchFresh)
                {
                    result = await GuaranteeFundingAsync(order, operation, plan, cancellationToken);
                }
                else
                {
                    var recovered = await _funding.RecoverGuaranteeAsync(
                        new ExchangeFundingRecoveryRequest(
                            _keys.FundingGuarantee(operation, plan), order.Id, operation.OperationId),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await GuaranteeFundingAsync(order, operation, plan, cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? ExchangeFundingEvidencePolicy.GuaranteeContradiction(plan, result)
                : null;

            await _plans.RecordFundingGuaranteeOutcomeAsync(
                operation.OperationId, result.Outcome, result.ProviderReference, contradiction ?? result.Detail,
                cancellationToken);

            var recordedPlan = plan with
            {
                FundingGuaranteeOutcome = result.Outcome,
                FundingGuaranteeReference = result.ProviderReference ?? plan.FundingGuaranteeReference,
                FundingGuaranteeDetail = contradiction ?? result.Detail ?? plan.FundingGuaranteeDetail
            };

            if (contradiction is not null)
                return await ReconcileAsync(
                    order, operation, predecessor, recordedPlan, isReplay, cancellationToken);

            if (result.Outcome == ProviderOperationOutcome.Rejected)
                return await SettleTerminalAsync(
                    order, operation, predecessor, recordedPlan, ExchangeDocumentOutcome.NotAttempted, isReplay,
                    cancellationToken);

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return await SettleAsync(
                    order, operation, predecessor, recordedPlan,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.NotAttempted,
                    isReplay,
                    cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return await EnterReservationAsync(
                order, operation, predecessor, recordedPlan, dispatchFresh: true, isReplay, cancellationToken);
        }

        private async Task<ExchangeFundingResult> GuaranteeFundingAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            CancellationToken cancellationToken)
            => await _funding.GuaranteeAsync(
                new ExchangeFundingGuaranteeRequest(
                    _keys.FundingGuarantee(operation, plan),
                    order.Id,
                    operation.OperationId,
                    plan.QuotedExchangeId,
                    plan.PredecessorDocumentNumber,
                    plan.PredecessorTravellerId,
                    plan.AddCollect!.Amount,
                    plan.AddCollect.CurrencyId,
                    plan.FundingMethodRef!),
                cancellationToken);

        private async Task<ExchangeOutcome> ReleaseThenSettleAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ExchangeFundingReleaseReason reason,
            bool dispatchFresh,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.IsFundingReleaseRejected)
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            if (!plan.RequiresFunding || !plan.IsFundingGuaranteed || plan.IsFundingReleased)
                return await SettleReleasedAsync(
                    order, operation, predecessor, plan, reason, isReplay, cancellationToken);

            ExchangeFundingResult result;

            try
            {
                if (dispatchFresh)
                {
                    result = await DispatchReleaseAsync(order, operation, plan, reason, cancellationToken);
                }
                else
                {
                    var recovered = await _funding.RecoverReleaseAsync(
                        new ExchangeFundingRecoveryRequest(
                            _keys.FundingRelease(operation, plan), order.Id, operation.OperationId),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await DispatchReleaseAsync(order, operation, plan, reason, cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            await _plans.RecordFundingReleaseOutcomeAsync(
                operation.OperationId, result.Outcome, result.Detail, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var released = plan with
            {
                FundingReleaseOutcome = result.Outcome,
                FundingReleaseDetail = result.Detail ?? plan.FundingReleaseDetail
            };

            return result.Outcome switch
            {
                ProviderOperationOutcome.Confirmed => await SettleReleasedAsync(
                    order, operation, predecessor, released, reason, isReplay, cancellationToken),
                ProviderOperationOutcome.Rejected => await ReconcileAsync(
                    order, operation, predecessor, released, isReplay, cancellationToken),
                _ => await SettleAsync(
                    order, operation, predecessor, released,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    DocumentOutcomeOf(reason),
                    isReplay,
                    cancellationToken)
            };
        }

        private async Task<ExchangeFundingResult> DispatchReleaseAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            ExchangeFundingReleaseReason reason,
            CancellationToken cancellationToken)
            => await _funding.ReleaseAsync(
                new ExchangeFundingReleaseRequest(
                    _keys.FundingRelease(operation, plan),
                    order.Id,
                    operation.OperationId,
                    plan.QuotedExchangeId,
                    plan.FundingGuaranteeReference,
                    reason),
                cancellationToken);

        private async Task<ExchangeOutcome> SettleReleasedAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ExchangeFundingReleaseReason reason,
            bool isReplay,
            CancellationToken cancellationToken)
            => reason == ExchangeFundingReleaseReason.DocumentRejected
                ? await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken)
                : await SettleTerminalAsync(
                    order, operation, predecessor, plan, DocumentOutcomeOf(reason), isReplay, cancellationToken);

        private static ExchangeDocumentOutcome DocumentOutcomeOf(ExchangeFundingReleaseReason reason) => reason switch
        {
            ExchangeFundingReleaseReason.DocumentRejected => ExchangeDocumentOutcome.Rejected,
            ExchangeFundingReleaseReason.DocumentDenied => ExchangeDocumentOutcome.Denied,
            _ => ExchangeDocumentOutcome.NotAttempted
        };

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
                return await FinalizeAsync(
                    order, operation, predecessor, plan, documentJustConfirmed: false, isReplay, cancellationToken);

            if (!plan.CanReproduceDocumentRequest)
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            if (!plan.IsFundingAssured)
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

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
                            _keys.DocumentExchange(operation, plan),
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
                    _keys.DocumentExchange(operation, plan),
                    order.Id,
                    operation.OperationId,
                    plan.PredecessorDocumentNumber,
                    plan.QuotedExchangeId,
                    plan.TargetSelectionRef,
                    plan.SourcePricingReference,
                    plan.Coupons.Select(DocumentCouponRequest).ToList(),
                    plan.RequiresDocumentCoupledResidual
                        ? new ExchangeCoupledResidualRequest(
                            plan.Residual!.Amount,
                            plan.Residual.CurrencyId,
                            plan.Residual.Disposition,
                            plan.Residual.ExpectedInstrument)
                        : null),
                cancellationToken);

        private static DocumentExchangeCouponRequest DocumentCouponRequest(AcceptedExchangePlanCoupon coupon)
            => new(coupon.PredecessorCouponNumber, coupon.Disposition, coupon.TicketedSegment);

        private async Task<ExchangeOutcome> AfterDocumentExchangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            DocumentExchangeResult result,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (ExchangeSuccessorEvidencePolicy.ContradictsDurableEvidence(plan, result))
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            await _plans.RecordDocumentExchangeOutcomeAsync(
                operation.OperationId, result.Outcome, result.ProviderReference, result.Successor, result.Detail,
                cancellationToken);

            var confirmed = result.Outcome == ProviderOperationOutcome.Confirmed;

            var residualContradiction = confirmed
                ? ExchangeSettlementEvidencePolicy.ResidualEvidenceContradiction(plan, result.Residual)
                    ?? await ResidualDocumentConflictAsync(
                        order, operation, predecessor, plan, result.Residual, cancellationToken)
                : null;

            var settlesCoupledResidual = confirmed && plan.RequiresDocumentCoupledResidual;

            if (confirmed && (settlesCoupledResidual || residualContradiction is not null))
            {
                await _plans.RecordResidualOutcomeAsync(
                    operation.OperationId,
                    residualContradiction is null
                        ? ProviderOperationOutcome.Confirmed
                        : ProviderOperationOutcome.Rejected,
                    result.Residual?.ProviderReference ?? result.ProviderReference,
                    result.Residual?.DocumentNumber,
                    result.Residual?.Instrument,
                    residualContradiction,
                    cancellationToken);

                if (residualContradiction is null && settlesCoupledResidual)
                    await MaterializeResidualDocumentAsync(
                        order, operation, predecessor, result.Residual!, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var recorded = plan with
            {
                DocumentExchangeOutcome = result.Outcome,
                DocumentExchangeProviderReference = result.ProviderReference ?? plan.DocumentExchangeProviderReference,
                DocumentExchangeDetail = result.Detail ?? plan.DocumentExchangeDetail,
                Successor = result.Successor ?? plan.Successor
            };

            if (confirmed && (settlesCoupledResidual || residualContradiction is not null))
                recorded = recorded with
                {
                    ResidualOutcome = residualContradiction is null
                        ? ProviderOperationOutcome.Confirmed
                        : ProviderOperationOutcome.Rejected,
                    ResidualProviderReference = result.Residual?.ProviderReference ?? result.ProviderReference,
                    ResidualInstrumentReference = residualContradiction is null
                        ? result.Residual?.DocumentNumber
                        : recorded.ResidualInstrumentReference,
                    ResidualInstrument = residualContradiction is null
                        ? result.Residual?.Instrument
                        : recorded.ResidualInstrument,
                    ResidualDetail = residualContradiction ?? recorded.ResidualDetail
                };

            return result.Outcome switch
            {
                ProviderOperationOutcome.Confirmed
                    => await FinalizeAsync(
                        order, operation, predecessor, recorded, documentJustConfirmed: true, isReplay,
                        cancellationToken),
                ProviderOperationOutcome.Rejected
                    => await ReleaseThenSettleAsync(
                        order, operation, predecessor, recorded, ExchangeFundingReleaseReason.DocumentRejected,
                        dispatchFresh: true, isReplay, cancellationToken),
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
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.Successor is not { } successor
                || !await IsUsableSuccessorIdentityAsync(order, operation, predecessor, plan, successor, cancellationToken))
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            var materialization = await MaterializeAsync(
                order, operation, predecessor, plan, successor, cancellationToken);
            var materialized = materialization.Materialized;

            if (materialization.Conflict is not null)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            if (plan.IsResidualRejected)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            if (plan.RequiresMonetarySettlement && !plan.IsMonetarySettled)
                return await SettleMonetaryAsync(
                    order, operation, predecessor, plan, successor, materialized, documentJustConfirmed, isReplay,
                    cancellationToken);

            if (plan.RequiresAncillaryReassociation && !plan.IsAncillarySettled)
                return await ReassociateAncillaryAsync(
                    order, operation, predecessor, plan, successor, materialized, documentJustConfirmed, isReplay,
                    cancellationToken);

            return await CompleteAsync(order, operation, predecessor, plan, materialized, isReplay, cancellationToken);
        }

        private async Task<MaterializationResult> MaterializeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            CancellationToken cancellationToken)
        {
            if (await AlreadyMaterializedAsync(order, operation, plan, cancellationToken) is { } already)
            {
                if (await ResumeDisassociationConflictAsync(operation, plan, cancellationToken) is { } conflict)
                    return new MaterializationResult(already, conflict);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return new MaterializationResult(already, null);
            }

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
                plan.SuccessorElectronicTicketId,
                successor.DocumentNumber,
                plan.Coupons
                    .Select(coupon => new ExchangedCouponLineage(
                        coupon.PredecessorTicketCouponId,
                        coupon.SuccessorTicketCouponId,
                        SuccessorCouponAttribution.Require(successor, coupon),
                        coupon.ServiceAfterExchange))
                    .ToList(),
                _idGenerator,
                _clock);

            var exchanged = order.CommitExchange(staged, _idGenerator, _clock);

            var successorTicket = ElectronicTicket.IssueSuccessor(
                SuccessorIssuance(order, predecessor, plan, successor, exchanged), _idGenerator, _clock);

            await _tickets.AddAsync(successorTicket, cancellationToken);

            await DisassociateAncillariesAsync(operation, plan, cancellationToken);

            if (plan.HasUnsettledDownstreamStage)
            {
                await _projector.ProjectAsync(order.Id, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new MaterializationResult(
                new MaterializedExchange(successorTicket, exchanged.OrderChangeId, exchanged.PriceChangeSetId),
                null);
        }

        private async Task<string?> ResumeDisassociationConflictAsync(
            OrderOperation operation,
            AcceptedExchangePlan plan,
            CancellationToken cancellationToken)
        {
            foreach (var disposition in plan.ExecutableAncillaries)
            {
                var document = await _miscDocuments.GetAsync(disposition.ElectronicMiscDocumentId, cancellationToken)
                               ?? throw ExceptionFactory.ElectronicMiscDocumentNotFound(
                                   disposition.ElectronicMiscDocumentId);

                if (document.IsDisassociationSettledBy(disposition.EmdCouponNumber, operation.OperationId))
                    continue;

                if (!document.PermitsDisassociation(
                        disposition.EmdCouponNumber,
                        operation.OperationId,
                        disposition.PredecessorTicketCouponId))
                    return $"coupon {disposition.EmdCouponNumber} of miscellaneous document "
                           + $"{disposition.EmdDocumentNumber} is no longer detached by this reissue";
            }

            await DisassociateAncillariesAsync(operation, plan, cancellationToken);

            return null;
        }

        private async Task<MaterializedExchange?> AlreadyMaterializedAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            CancellationToken cancellationToken)
            => CommittedExchange(order, operation.OperationId) is { } committed
                ? new MaterializedExchange(
                    await _tickets.GetAsync(plan.SuccessorElectronicTicketId, cancellationToken)
                    ?? throw ExceptionFactory.AccountableDocumentNotFound(
                        plan.SuccessorElectronicTicketId, order.Id),
                    committed.Id,
                    order.OriginatingPriceConsequenceOf(committed.Id)!.Id)
                : null;

        private async Task<string?> ResidualDocumentConflictAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ResidualDocumentIdentity? residual,
            CancellationToken cancellationToken)
        {
            if (residual is null || !plan.RequiresDocumentCoupledResidual)
                return null;

            var existing = await FindMiscDocumentAsync(order.Id, residual.DocumentNumber, cancellationToken);

            return existing is null
                ? null
                : ResidualDocumentIdentityPolicy.Conflict(
                    existing, residual, operation.OperationId, predecessor.TravelerId);
        }

        private async Task<ElectronicMiscDocument?> FindMiscDocumentAsync(
            long orderId,
            string documentNumber,
            CancellationToken cancellationToken)
            => (await _miscDocuments.ListByOrderAsync(orderId, cancellationToken))
                .FirstOrDefault(document =>
                    string.Equals(document.DocumentNumber, documentNumber, StringComparison.Ordinal));

        private async Task MaterializeResidualDocumentAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            ResidualDocumentIdentity residual,
            CancellationToken cancellationToken)
        {
            if (await FindMiscDocumentAsync(order.Id, residual.DocumentNumber, cancellationToken) is not null)
                return;

            await _miscDocuments.AddAsync(
                ElectronicMiscDocument.Issue(
                    _idGenerator.NewId(),
                    order.Id,
                    predecessor.TravelerId,
                    operation.OperationId,
                    residual.DocumentNumber,
                    ElectronicMiscDocumentType.Standalone,
                    residual.ReasonForIssuanceCode,
                    residual.IssuerCarrierId,
                    residual.IssuingOfficeId,
                    residual.Authority,
                    residual.CurrencyId,
                    [
                        new EmdCouponIssuance(
                            EmdCouponPurpose.ResidualValue,
                            residual.ReasonForIssuanceSubCode,
                            residual.Amount,
                            [],
                            ExternalValueReference: residual.DocumentNumber)
                    ],
                    _idGenerator,
                    _clock),
                cancellationToken);
        }

        private async Task DisassociateAncillariesAsync(
            OrderOperation operation,
            AcceptedExchangePlan plan,
            CancellationToken cancellationToken)
        {
            foreach (var disposition in plan.ExecutableAncillaries)
            {
                var document = await _miscDocuments.GetAsync(disposition.ElectronicMiscDocumentId, cancellationToken)
                               ?? throw ExceptionFactory.ElectronicMiscDocumentNotFound(
                                   disposition.ElectronicMiscDocumentId);

                if (document.IsDisassociationSettledBy(disposition.EmdCouponNumber, operation.OperationId))
                    continue;

                if (!document.PermitsDisassociation(
                        disposition.EmdCouponNumber,
                        operation.OperationId,
                        disposition.PredecessorTicketCouponId))
                    throw ExceptionFactory.ElectronicMiscDocumentAssociationMoved(
                        disposition.EmdDocumentNumber,
                        disposition.EmdCouponNumber,
                        disposition.PredecessorDocumentNumber);

                document.DisassociateCouponByReissue(
                    new EmdCouponDisassociation(
                        disposition.EmdCouponNumber,
                        disposition.PredecessorTicketCouponId,
                        disposition.PredecessorDocumentNumber,
                        disposition.PredecessorCouponNumber,
                        operation.OperationId,
                        disposition.DecisionReference,
                        plan.DocumentExchangeProviderReference),
                    _idGenerator,
                    _clock);
            }
        }

        private async Task<ExchangeOutcome> CompleteAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            MaterializedExchange materialized,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Completed,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Completed, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            await _projector.ProjectAsync(order.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ExchangeOutcomeFactory.Create(
                order, operation, predecessor, plan, materialized.Successor,
                materialized.OrderChangeId, materialized.PriceChangeSetId,
                ServicingOperationStatus.Completed, ExchangeDocumentOutcome.Exchanged, isReplay);
        }

        private async Task<ExchangeOutcome> SettleMonetaryAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.RequiresFunding && !plan.IsFundingCaptured)
                return await CaptureFundingAsync(
                    order, operation, predecessor, plan, successor, materialized, documentJustConfirmed, isReplay,
                    cancellationToken);

            if (!plan.IsCollectionSettled)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            if (plan.RequiresRefundDue && !plan.IsRefundDueSettled)
                return await SettleRefundDueAsync(
                    order, operation, predecessor, plan, successor, materialized, documentJustConfirmed, isReplay,
                    cancellationToken);

            if (plan.RequiresResidual && !plan.IsResidualSettled)
                return plan.RequiresExternalResidual
                    ? await SettleResidualAsync(
                        order, operation, predecessor, plan, successor, materialized, documentJustConfirmed, isReplay,
                        cancellationToken)
                    : await ReconcileAsync(
                        order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            return await ReconcileAsync(
                order, operation, predecessor, plan, isReplay, cancellationToken, materialized);
        }

        private async Task<ExchangeOutcome> SettleRefundDueAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (!plan.CanReproduceRefundDueRequest)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            RefundValueResult result;

            try
            {
                if (documentJustConfirmed)
                {
                    result = await DispatchRefundDueAsync(order, operation, plan, successor, cancellationToken);
                }
                else
                {
                    var recovered = await _refundValues.RecoverAsync(
                        new RefundValueRecoveryRequest(_keys.RefundDue(operation, plan), order.Id, operation.OperationId),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await DispatchRefundDueAsync(order, operation, plan, successor, cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? ExchangeSettlementEvidencePolicy.RefundDueContradiction(plan, result)
                : null;

            await _plans.RecordRefundDueOutcomeAsync(
                operation.OperationId, result.Outcome, result.ValueMovementReference, contradiction ?? result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan with
            {
                RefundDueOutcome = result.Outcome,
                RefundDueReference = result.ValueMovementReference ?? plan.RefundDueReference,
                RefundDueDetail = contradiction ?? result.Detail ?? plan.RefundDueDetail
            };

            return await AfterMonetarySettlementAsync(
                order, operation, predecessor, settled, materialized, result.Outcome, contradiction, isReplay,
                cancellationToken);
        }

        private async Task<RefundValueResult> DispatchRefundDueAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            CancellationToken cancellationToken)
            => await _refundValues.RequestAsync(
                new RefundValueRequest(
                    _keys.RefundDue(operation, plan),
                    order.Id,
                    operation.OperationId,
                    plan.PredecessorDocumentNumber,
                    plan.RefundDue!.Amount,
                    plan.RefundDue.CurrencyId,
                    plan.RefundDue.Disposition,
                    null,
                    successor.DocumentNumber,
                    plan.SourcePricingReference),
                cancellationToken);

        private async Task<ExchangeOutcome> SettleResidualAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (!plan.CanReproduceResidualRequest)
                return await ReconcileAsync(order, operation, predecessor, plan, isReplay, cancellationToken);

            ExchangeResidualResult result;

            try
            {
                if (documentJustConfirmed)
                {
                    result = await DispatchResidualAsync(order, operation, plan, successor, cancellationToken);
                }
                else
                {
                    var recovered = await _residuals.RecoverAsync(
                        new ExchangeResidualRecoveryRequest(
                            _keys.Residual(operation, plan), order.Id, operation.OperationId),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await DispatchResidualAsync(order, operation, plan, successor, cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? ExchangeSettlementEvidencePolicy.ResidualContradiction(plan, result)
                : null;

            await _plans.RecordResidualOutcomeAsync(
                operation.OperationId,
                result.Outcome,
                result.ProviderReference,
                result.InstrumentReference,
                result.Instrument,
                contradiction ?? result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan with
            {
                ResidualOutcome = result.Outcome,
                ResidualProviderReference = result.ProviderReference ?? plan.ResidualProviderReference,
                ResidualInstrumentReference = result.InstrumentReference ?? plan.ResidualInstrumentReference,
                ResidualInstrument = result.Instrument ?? plan.ResidualInstrument,
                ResidualDetail = contradiction ?? result.Detail ?? plan.ResidualDetail
            };

            return await AfterMonetarySettlementAsync(
                order, operation, predecessor, settled, materialized, result.Outcome, contradiction, isReplay,
                cancellationToken);
        }

        private async Task<ExchangeResidualResult> DispatchResidualAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            CancellationToken cancellationToken)
            => await _residuals.FulfillAsync(
                new ExchangeResidualRequest(
                    _keys.Residual(operation, plan),
                    order.Id,
                    operation.OperationId,
                    plan.QuotedExchangeId,
                    plan.PredecessorDocumentNumber,
                    successor.DocumentNumber,
                    plan.PredecessorTravellerId,
                    plan.Residual!.Amount,
                    plan.Residual.CurrencyId,
                    plan.Residual.Disposition,
                    plan.Residual.ExpectedInstrument,
                    plan.SourcePricingReference),
                cancellationToken);

        private async Task<ExchangeOutcome> ReassociateAncillaryAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var pending = plan.NextUnsettledAncillary;

            if (pending is null)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            if (pending.IsRetention)
                return await RetainAncillaryResidualAsync(
                    order, operation, predecessor, plan, successor, materialized, pending, isReplay,
                    cancellationToken);

            if (pending.IsEmdExchange)
                return await ExchangeAncillaryToNewEmdAsync(
                    order, operation, predecessor, plan, successor, materialized, pending, documentJustConfirmed,
                    isReplay, cancellationToken);

            if (pending.IsRefund)
                return await RefundAncillaryAsync(
                    order, operation, predecessor, plan, successor, materialized, pending, documentJustConfirmed,
                    isReplay, cancellationToken);

            if (!TargetCouponNumber(plan, successor, pending, out var successorCouponNumber))
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var document = await _miscDocuments.GetAsync(pending.ElectronicMiscDocumentId, cancellationToken)
                           ?? throw ExceptionFactory.ElectronicMiscDocumentNotFound(
                               pending.ElectronicMiscDocumentId);

            if (!document.PermitsReassociation(
                    pending.EmdCouponNumber,
                    operation.OperationId,
                    pending.TargetSuccessorTicketCouponId!.Value))
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            EmdAssociationResult result;

            try
            {
                if (documentJustConfirmed)
                {
                    result = await DispatchReassociationAsync(
                        order, operation, document, plan, successor, pending, successorCouponNumber, cancellationToken);
                }
                else
                {
                    var recovered = await _emdAssociations.RecoverReassociationAsync(
                        new EmdAssociationRecoveryRequest(
                            _keys.Reassociation(operation, pending),
                            order.Id,
                            operation.OperationId,
                            pending.EmdDocumentNumber,
                            pending.EmdCouponNumber),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await DispatchReassociationAsync(
                            order, operation, document, plan, successor, pending, successorCouponNumber,
                            cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? ExchangeSettlementEvidencePolicy.ReassociationContradiction(
                    pending, successor.DocumentNumber, successorCouponNumber, result)
                : null;

            await _plans.RecordAncillaryAssociationOutcomeAsync(
                operation.OperationId,
                pending.EmdCouponId,
                result.Outcome,
                result.ProviderReference,
                contradiction ?? result.Detail,
                cancellationToken);

            if (contradiction is null && result.Outcome == ProviderOperationOutcome.Confirmed)
                document.ReassociateCoupon(
                    new EmdCouponReassociation(
                        pending.EmdCouponNumber,
                        pending.PredecessorTicketCouponId,
                        pending.PredecessorDocumentNumber,
                        pending.PredecessorCouponNumber,
                        pending.TargetSuccessorTicketCouponId.Value,
                        successor.DocumentNumber,
                        successorCouponNumber,
                        operation.OperationId,
                        pending.DecisionReference,
                        result.ProviderReference),
                    _idGenerator,
                    _clock);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan with
            {
                AncillaryDispositions = plan.Ancillaries
                    .Select(disposition => disposition.EmdCouponId == pending.EmdCouponId
                        ? disposition with
                        {
                            AssociationOutcome = result.Outcome,
                            AssociationProviderReference =
                                result.ProviderReference ?? disposition.AssociationProviderReference,
                            AssociationDetail =
                                contradiction ?? result.Detail ?? disposition.AssociationDetail
                        }
                        : disposition)
                    .ToList()
            };

            if (contradiction is not null || result.Outcome == ProviderOperationOutcome.Rejected)
                return await ReconcileAsync(
                    order, operation, predecessor, settled, isReplay, cancellationToken, materialized);

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return await SettleAsync(
                    order, operation, predecessor, settled,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);

            return settled.IsAncillarySettled
                ? await CompleteAsync(
                    order, operation, predecessor, settled, materialized, isReplay, cancellationToken)
                : await ReassociateAncillaryAsync(
                    order, operation, predecessor, settled, successor, materialized,
                    documentJustConfirmed: false, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> RefundAncillaryAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryDisposition pending,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var document = await _miscDocuments.GetAsync(pending.ElectronicMiscDocumentId, cancellationToken)
                           ?? throw ExceptionFactory.ElectronicMiscDocumentNotFound(
                               pending.ElectronicMiscDocumentId);

            if (!document.PermitsRefund(pending.EmdCouponNumber, operation.OperationId))
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            return pending.IsRefundDocumentSettled
                ? await MoveAncillaryRefundValueAsync(
                    order, operation, predecessor, plan, successor, materialized, pending, isReplay,
                    cancellationToken)
                : await RefundAncillaryDocumentAsync(
                    order, operation, predecessor, plan, successor, materialized, pending, document,
                    documentJustConfirmed, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> RefundAncillaryDocumentAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryDisposition pending,
            ElectronicMiscDocument document,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var key = _keys.AncillaryRefund(operation, pending);
            DocumentRefundResult result;

            try
            {
                if (documentJustConfirmed)
                {
                    result = await DispatchAncillaryRefundAsync(order, operation, pending, key, cancellationToken);
                }
                else
                {
                    var recovered = await _documentRefunds.RecoverAsync(
                        new DocumentRefundRecoveryRequest(
                            key, order.Id, operation.OperationId, pending.EmdDocumentNumber),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await DispatchAncillaryRefundAsync(order, operation, pending, key, cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? ExchangeSettlementEvidencePolicy.AncillaryRefundDocumentContradiction(pending, result)
                : null;

            await _plans.RecordAncillaryRefundOutcomeAsync(
                operation.OperationId,
                pending.EmdCouponId,
                valueMovement: false,
                result.Outcome,
                result.ProviderReference,
                contradiction ?? result.Detail,
                cancellationToken);

            long? consequenceId = null;

            if (contradiction is null && result.Outcome == ProviderOperationOutcome.Confirmed)
            {
                document.RefundCoupon(
                    new EmdCouponRefund(
                        pending.EmdCouponNumber,
                        pending.RefundAmount!.Value,
                        pending.RefundCurrencyId!.Value,
                        pending.RefundDisposition!,
                        operation.OperationId,
                        pending.DecisionReference,
                        result.ProviderReference),
                    _clock);

                consequenceId = CommitAncillaryRefundConsequence(order, operation, pending);

                if (consequenceId is { } committed)
                    await _plans.RecordAncillaryRefundConsequenceAsync(
                        operation.OperationId, pending.EmdCouponId, committed, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = WithAncillaryRefund(
                plan, pending, valueMovement: false, result.Outcome, result.ProviderReference,
                contradiction ?? result.Detail, consequenceId);

            if (contradiction is not null || result.Outcome == ProviderOperationOutcome.Rejected)
                return await ReconcileAsync(
                    order, operation, predecessor, settled, isReplay, cancellationToken, materialized);

            return result.Outcome == ProviderOperationOutcome.Confirmed
                ? await MoveAncillaryRefundValueAsync(
                    order, operation, predecessor, settled, successor, materialized,
                    settled.Ancillaries.Single(candidate => candidate.EmdCouponId == pending.EmdCouponId),
                    isReplay, cancellationToken)
                : await SettleAsync(
                    order, operation, predecessor, settled,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);
        }

        private async Task<ExchangeOutcome> MoveAncillaryRefundValueAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryDisposition pending,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var key = _keys.AncillaryRefundValue(operation, pending);
            RefundValueResult result;

            try
            {
                var recovered = await _refundValues.RecoverAsync(
                    new RefundValueRecoveryRequest(key, order.Id, operation.OperationId),
                    cancellationToken);

                result = recovered.WasDispatched
                    ? recovered.AsResult()
                    : await _refundValues.RequestAsync(
                        new RefundValueRequest(
                            key,
                            order.Id,
                            operation.OperationId,
                            pending.EmdDocumentNumber,
                            pending.RefundAmount!.Value,
                            pending.RefundCurrencyId!.Value,
                            pending.RefundDisposition!,
                            pending.RefundSourceReference,
                            successor.DocumentNumber,
                            plan.SourcePricingReference),
                        cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? ExchangeSettlementEvidencePolicy.AncillaryRefundValueContradiction(pending, result)
                : null;

            await _plans.RecordAncillaryRefundOutcomeAsync(
                operation.OperationId,
                pending.EmdCouponId,
                valueMovement: true,
                result.Outcome,
                result.ValueMovementReference,
                contradiction ?? result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = WithAncillaryRefund(
                plan, pending, valueMovement: true, result.Outcome, result.ValueMovementReference,
                contradiction ?? result.Detail);

            if (contradiction is not null || result.Outcome == ProviderOperationOutcome.Rejected)
                return await ReconcileAsync(
                    order, operation, predecessor, settled, isReplay, cancellationToken, materialized);

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return await SettleAsync(
                    order, operation, predecessor, settled,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);

            return settled.IsAncillarySettled
                ? await CompleteAsync(
                    order, operation, predecessor, settled, materialized, isReplay, cancellationToken)
                : await ReassociateAncillaryAsync(
                    order, operation, predecessor, settled, successor, materialized,
                    documentJustConfirmed: false, isReplay, cancellationToken);
        }

        private async Task<DocumentRefundResult> DispatchAncillaryRefundAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangeAncillaryDisposition pending,
            string operationKey,
            CancellationToken cancellationToken)
            => await _documentRefunds.RefundAsync(
                new DocumentRefundRequest(
                    operationKey,
                    order.Id,
                    operation.OperationId,
                    pending.EmdDocumentNumber,
                    [pending.EmdCouponNumber]),
                cancellationToken);

        private static AcceptedExchangePlan WithAncillaryRefund(
            AcceptedExchangePlan plan,
            AcceptedExchangeAncillaryDisposition pending,
            bool valueMovement,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            long? priceChangeSetId = null)
            => plan with
            {
                AncillaryDispositions = plan.Ancillaries
                    .Select(disposition => disposition.EmdCouponId != pending.EmdCouponId
                        ? disposition
                        : valueMovement
                            ? disposition with
                            {
                                RefundValueOutcome = outcome,
                                RefundValueReference = providerReference ?? disposition.RefundValueReference,
                                RefundValueDetail = detail ?? disposition.RefundValueDetail
                            }
                            : disposition with
                            {
                                RefundDocumentOutcome = outcome,
                                RefundDocumentReference = providerReference ?? disposition.RefundDocumentReference,
                                RefundDocumentDetail = detail ?? disposition.RefundDocumentDetail,
                                RefundPriceChangeSetId =
                                    priceChangeSetId ?? disposition.RefundPriceChangeSetId
                            })
                    .ToList()
            };

        private long? CommitAncillaryRefundConsequence(
            Order order,
            OrderOperation operation,
            AcceptedExchangeAncillaryDisposition refunded)
        {
            if (refunded.IsRefundConsequenceCommitted)
                return null;

            var consequence = order.CommitDependentPriceChange(
                new AcceptedDependentPriceChangeArgs(
                    operation.OperationId,
                    OrderChangeType.Exchange,
                    PriceChangeReason.Refund,
                    refunded.RefundPricingSource!.Value,
                    (refunded.RefundPricingLines ?? [])
                        .Select(AncillaryRefundPricingLine)
                        .ToList(),
                    SourcePricingRef: refunded.RefundSourceReference),
                _idGenerator,
                _clock);

            order.ApplyDocumentRefund(
                refunded.RefundedOrderServiceId is { } orderServiceId ? [orderServiceId] : [],
                _clock);

            return consequence.Id;
        }

        private static AcceptedPricingLineArgs AncillaryRefundPricingLine(AcceptedRefundPricingLine line)
            => new(
                line.ComponentType,
                line.Effect,
                line.Direction,
                line.LineRole,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                line.BasisType,
                line.Refundability,
                OrderItemId: line.OrderItemId,
                Code: line.Code,
                Description: line.Description,
                ExchangeRate: line.ExchangeRate,
                ApplicationLevel: line.ApplicationLevel,
                BasisReferenceId: line.BasisReferenceId,
                SourceLineRef: line.SourceLineRef,
                OccurrenceKey: line.OccurrenceKey,
                OriginalPricingLineId: line.ReversesPricingLineId,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory);

        private async Task<EmdAssociationResult> DispatchReassociationAsync(
            Order order,
            OrderOperation operation,
            ElectronicMiscDocument document,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            AcceptedExchangeAncillaryDisposition disposition,
            int successorCouponNumber,
            CancellationToken cancellationToken)
            => await _emdAssociations.ReassociateAsync(
                new EmdReassociationRequest(
                    _keys.Reassociation(operation, disposition),
                    order.Id,
                    operation.OperationId,
                    disposition.EmdDocumentNumber,
                    disposition.EmdCouponNumber,
                    disposition.PredecessorDocumentNumber,
                    disposition.PredecessorCouponNumber,
                    successor.DocumentNumber,
                    successorCouponNumber,
                    plan.PredecessorTravellerId,
                    document.IssuerCarrierId,
                    disposition.DecisionReference),
                cancellationToken);

        private static bool TargetCouponNumber(
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            AcceptedExchangeAncillaryDisposition disposition,
            out int successorCouponNumber)
        {
            var target = plan.Coupons.FirstOrDefault(coupon =>
                disposition.TargetSuccessorTicketCouponId is { } couponId
                && coupon.SuccessorTicketCouponId == couponId);

            successorCouponNumber = target is null ? 0 : SuccessorCouponAttribution.Require(successor, target);

            return target is not null;
        }

        private async Task<ExchangeOutcome> AfterMonetarySettlementAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan settled,
            MaterializedExchange materialized,
            ProviderOperationOutcome outcome,
            string? contradiction,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (contradiction is not null || outcome == ProviderOperationOutcome.Rejected)
                return await ReconcileAsync(
                    order, operation, predecessor, settled, isReplay, cancellationToken, materialized);

            return outcome == ProviderOperationOutcome.Confirmed
                ? await FinalizeAsync(
                    order, operation, predecessor, settled, documentJustConfirmed: false, isReplay, cancellationToken)
                : await SettleAsync(
                    order, operation, predecessor, settled,
                    ServicingOperationStatus.AwaitingExternal,
                    outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);
        }

        private async Task<ExchangeOutcome> CaptureFundingAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (!plan.CanReproduceFundingRequest)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            ExchangeFundingResult result;

            try
            {
                if (documentJustConfirmed)
                {
                    result = await CaptureAsync(order, operation, plan, successor, cancellationToken);
                }
                else
                {
                    var recovered = await _funding.RecoverCaptureAsync(
                        new ExchangeFundingRecoveryRequest(
                            _keys.FundingCapture(operation, plan), order.Id, operation.OperationId),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await CaptureAsync(order, operation, plan, successor, cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var contradiction = result.Outcome == ProviderOperationOutcome.Confirmed
                ? ExchangeFundingEvidencePolicy.CaptureContradiction(plan, result)
                : null;

            await _plans.RecordFundingCaptureOutcomeAsync(
                operation.OperationId, result.Outcome, result.ProviderReference, contradiction ?? result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan with
            {
                FundingCaptureOutcome = result.Outcome,
                FundingCaptureReference = result.ProviderReference ?? plan.FundingCaptureReference,
                FundingCaptureDetail = contradiction ?? result.Detail ?? plan.FundingCaptureDetail
            };

            if (contradiction is not null || result.Outcome == ProviderOperationOutcome.Rejected)
                return await ReconcileAsync(
                    order, operation, predecessor, settled, isReplay, cancellationToken, materialized);

            return result.Outcome == ProviderOperationOutcome.Confirmed
                ? await FinalizeAsync(
                    order, operation, predecessor, settled, documentJustConfirmed: true, isReplay, cancellationToken)
                : await SettleAsync(
                    order, operation, predecessor, settled,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);
        }

        private async Task<ExchangeFundingResult> CaptureAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            CancellationToken cancellationToken)
            => await _funding.CaptureAsync(
                new ExchangeFundingCaptureRequest(
                    _keys.FundingCapture(operation, plan),
                    order.Id,
                    operation.OperationId,
                    plan.QuotedExchangeId,
                    successor.DocumentNumber,
                    plan.FundingGuaranteeReference,
                    plan.AddCollect!.Amount,
                    plan.AddCollect.CurrencyId),
                cancellationToken);

        private static SuccessorTicketIssuance SuccessorIssuance(
            Order order,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            ExchangedOrder exchanged)
            => new(
                plan.SuccessorElectronicTicketId,
                predecessor.Id,
                plan.OperationId,
                order.Id,
                predecessor.TravelerId,
                successor.DocumentNumber,
                successor.IssuerCarrierId,
                successor.IssuingOfficeId,
                successor.Authority,
                successor.VoidDeadline,
                plan.SaleCurrencyId,
                plan.Coupons
                    .Select(coupon => SuccessorCouponIssuance(plan, successor, exchanged, coupon))
                    .ToList());

        private static SuccessorCouponIssuance SuccessorCouponIssuance(
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            ExchangedOrder exchanged,
            AcceptedExchangePlanCoupon coupon)
        {
            var accepted = AcceptedCoupon(plan, coupon);
            var binding = exchanged.Coupons.Single(candidate => candidate.PredecessorTicketCouponId == coupon.PredecessorTicketCouponId);

            return new SuccessorCouponIssuance(
                coupon.SuccessorTicketCouponId,
                SuccessorCouponAttribution.Require(successor, coupon),
                coupon.PredecessorTicketCouponId,
                binding.OrderServiceId,
                binding.OrderSegmentId,
                IssuedSegment(coupon.TicketedSegment),
                accepted.Successor.FareBasis,
                accepted.Successor.IssuanceValue,
                accepted.Successor.PriceLinks
                    .Select(link => new TicketCouponPriceLink(
                        exchanged.PricingLineIdsBySourceRef[link.SourceLineRef],
                        null,
                        link.AttributedValue))
                    .ToList());
        }

        private static IssuedSegmentSnapshot IssuedSegment(TicketedSegmentSnapshot segment)
            => new(
                segment.MarketingAirlineId,
                segment.FlightNumber,
                segment.OriginAirportId,
                segment.DestinationAirportId,
                segment.DepartureDateTime,
                segment.ArrivalDateTime,
                segment.BookingClass);

        private static AcceptedExchangeCoupon AcceptedCoupon(AcceptedExchangePlan plan, AcceptedExchangePlanCoupon coupon)
            => plan.Accepted.Coupons.Single(candidate => candidate.PredecessorTicketCouponId == coupon.PredecessorTicketCouponId);

        private async Task<bool> IsUsableSuccessorIdentityAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(successor.DocumentNumber))
                return false;

            if (string.Equals(successor.DocumentNumber, predecessor.DocumentNumber, StringComparison.Ordinal))
                return false;

            if (!ExchangeSuccessorEvidencePolicy.CoversEveryCoupon(plan, successor))
                return false;

            if (CommittedExchange(order, operation.OperationId) is not null)
                return true;

            return await _tickets.FindByDocumentNumberAsync(successor.DocumentNumber, cancellationToken) is null;
        }

        private async Task<ServicingOperationStatus?> OperationStatusAsync(
            OrderOperation operation,
            CancellationToken cancellationToken)
            => (await _operationStore.FindAsync(operation.OperationId, cancellationToken))?.Status;

        private async Task<bool> AwaitsReconciliationAsync(OrderOperation operation, CancellationToken cancellationToken)
            => await OperationStatusAsync(operation, cancellationToken)
               == ServicingOperationStatus.NeedsReconciliation;

        private async Task<ExchangeOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            bool isReplay,
            CancellationToken cancellationToken,
            MaterializedExchange? materialized = null)
            => await SettleAsync(
                order, operation, predecessor, plan,
                ServicingOperationStatus.NeedsReconciliation,
                CommandReceiptStatus.NeedsReconciliation,
                plan.IsDocumentExchangeRejected ? ExchangeDocumentOutcome.Rejected : ExchangeDocumentOutcome.Pending,
                isReplay,
                cancellationToken,
                materialized);

        private async Task<ExchangeOutcome> SettleAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ServicingOperationStatus operationStatus,
            CommandReceiptStatus receiptStatus,
            ExchangeDocumentOutcome documentOutcome,
            bool isReplay,
            CancellationToken cancellationToken,
            MaterializedExchange? materialized = null)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId, operationStatus, operation.ClaimGeneration, cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, receiptStatus, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ExchangeOutcomeFactory.Create(
                order, operation, predecessor, plan,
                materialized?.Successor,
                materialized?.OrderChangeId,
                materialized?.PriceChangeSetId,
                operationStatus,
                materialized is null ? documentOutcome : ExchangeDocumentOutcome.Exchanged,
                isReplay);
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

            return ExchangeOutcomeFactory.Create(
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

            return ExchangeOutcomeFactory.Create(
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

            var changeSet = order.OriginatingPriceConsequenceOf(committed.Id)!;

            return ExchangeOutcomeFactory.Create(
                order, operation, predecessor, plan, successor, committed.Id, changeSet.Id,
                ServicingOperationStatus.Completed, ExchangeDocumentOutcome.Exchanged, true);
        }

        private static ExchangeQuoteRequest QuoteRequest(Order order, ExchangeScope scope)
            => new(
                order.Id,
                order.CommercialVersion,
                scope.PredecessorTicket.Id,
                scope.PredecessorTicket.DocumentNumber,
                scope.ChangedOrderServiceIds,
                scope.Coupons,
                scope.HistoricalUsedCoupons,
                order.PredecessorPricingEvidence(
                    scope.PredecessorTicket.Id,
                    scope.PredecessorTicket.DocumentNumber,
                    scope.PredecessorTicket.CarriedPricingLinks()),
                order.FareConstructionContexts(),
                order.CurrencyId);

        private AcceptedExchangePlan NewPlan(
            Order order,
            OrderOperation operation,
            ExchangeScope scope,
            string quotedExchangeId,
            int expectedCommercialVersion,
            AcceptedExchange accepted,
            string? fundingMethodRef)
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
                scope.PredecessorTicket.TravelerId,
                _idGenerator.NewId(),
                expectedCommercialVersion,
                accepted.MonetaryOutcome,
                accepted,
                scope.Coupons
                    .Select(coupon => PlanCoupon(coupon, accepted))
                    .ToList(),
                FundingMethodRef: fundingMethodRef);

        private AcceptedExchangePlanCoupon PlanCoupon(ExchangeScopeCoupon coupon, AcceptedExchange accepted)
        {
            var acceptedCoupon = accepted.Coupons.FirstOrDefault(candidate =>
                candidate.PredecessorTicketCouponId == coupon.PredecessorTicketCouponId);

            var usableAcceptedReplacement = coupon.ServiceIsChanging
                                            && acceptedCoupon is { IsReplaced: true, Replacement: not null };

            return new AcceptedExchangePlanCoupon(
                coupon.PredecessorTicketCouponId,
                coupon.CouponNumber,
                coupon.CurrentOrderServiceId,
                coupon.ServiceIsChanging ? ExchangeCouponDisposition.Replaced : ExchangeCouponDisposition.Continued,
                _idGenerator.NewId(),
                usableAcceptedReplacement
                    ? AcceptedTicketedSegment(acceptedCoupon!.Replacement!.Segment)
                    : coupon.CurrentSegment,
                usableAcceptedReplacement ? _idGenerator.NewId() : null,
                usableAcceptedReplacement ? _idGenerator.NewId() : null);
        }

        private static TicketedSegmentSnapshot AcceptedTicketedSegment(Domain.OrderAggregate.AcceptedSource.AcceptedSegment segment)
            => new(
                segment.MarketingAirlineId,
                segment.FlightNumber,
                segment.OriginAirportId,
                segment.DestinationAirportId,
                segment.DepartureAt,
                segment.ArrivalAt,
                segment.BookingClass);

        private AcceptedExchangeArgs ToArgs(AcceptedExchangePlan plan, ElectronicTicket predecessor)
            => new(
                plan.Accepted,
                plan.PredecessorTravellerId,
                plan.SuccessorElectronicTicketId,
                plan.Coupons
                    .Select(coupon => new ExchangeCouponAllocation(
                        coupon.PredecessorTicketCouponId,
                        coupon.SuccessorTicketCouponId,
                        coupon.ReplacementOrderServiceId,
                        coupon.ReplacementOrderSegmentId))
                    .ToList(),
                ExchangePricingCorrelation.Map(predecessor.Id, predecessor.CarriedPricingLineIds()),
                plan.OperationId,
                _callerContext.ActorId,
                CallerScope.For(_callerContext));

        private static Entities.OrderChange? CommittedExchange(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.Exchange);

    }
}
