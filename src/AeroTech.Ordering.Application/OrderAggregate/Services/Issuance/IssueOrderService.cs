using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.Funding;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed class IssueOrderService : IIssueOrderService
    {
        public const string FundingStep = "coverage";

        private readonly IOrderRepository _orders;
        private readonly IFulfillmentReservationRepository _reservations;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IElectronicMiscDocumentRepository _miscDocuments;
        private readonly IDocumentStockRepository _stocks;
        private readonly IFundingCoveragePort _funding;
        private readonly IElectronicTicketIssuer _ticketIssuer;
        private readonly IElectronicMiscDocumentIssuer _miscDocumentIssuer;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly IHomeOperatorProvider _homeOperator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;
        private readonly string _ticketDocumentType;
        private readonly string _emdDocumentType;

        public IssueOrderService(
            IOrderRepository orders,
            IFulfillmentReservationRepository reservations,
            IElectronicTicketRepository tickets,
            IElectronicMiscDocumentRepository miscDocuments,
            IDocumentStockRepository stocks,
            IFundingCoveragePort funding,
            IElectronicTicketIssuer ticketIssuer,
            IElectronicMiscDocumentIssuer miscDocumentIssuer,
            IOrderOperationCoordinator operations,
            IServicingOperationStore operationStore,
            ICommandReceiptStore receipts,
            IHomeOperatorProvider homeOperator,
            IUnitOfWork unitOfWork,
            IClock clock,
            IOrderProjector projector,
            IOptions<OrderOperationOptions> options)
        {
            _orders = orders;
            _reservations = reservations;
            _tickets = tickets;
            _miscDocuments = miscDocuments;
            _stocks = stocks;
            _funding = funding;
            _ticketIssuer = ticketIssuer;
            _miscDocumentIssuer = miscDocumentIssuer;
            _operations = operations;
            _operationStore = operationStore;
            _receipts = receipts;
            _homeOperator = homeOperator;
            _unitOfWork = unitOfWork;
            _clock = clock;
            _projector = projector;

            _ticketDocumentType = options.Value.TicketDocumentType
                ?? throw new InvalidOperationException(
                    $"'{OrderOperationOptions.SectionName}:{nameof(OrderOperationOptions.TicketDocumentType)}' must be configured.");

            _emdDocumentType = options.Value.EmdDocumentType
                ?? throw new InvalidOperationException(
                    $"'{OrderOperationOptions.SectionName}:{nameof(OrderOperationOptions.EmdDocumentType)}' must be configured.");
        }

        public async Task<IssueOrderOutcome> IssueAsync(
            long orderId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
        {
            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            if (expectedCommercialVersion is { } expected && expected != order.CommercialVersion)
                throw ExceptionFactory.OrderCommercialVersionMismatch(expected, orderId, order.CommercialVersion);

            var ownerAirlineId = await _homeOperator.GetOwnerAirlineIdAsync(cancellationToken);

            var operation = await _operations.BeginAsync(
                orderId,
                ServicingOperationKind.Issue,
                idempotencyKey,
                new { Operation = "Issue", OrderId = orderId },
                expectedCommercialVersion,
                cancellationToken);

            var orderTickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);
            var orderDocuments = await _miscDocuments.ListByOrderAsync(orderId, cancellationToken);

            order.RecordIssuedDocuments(ElectronicTicketIssuer.DocumentEvidenceFrom(orderTickets));

            foreach (var document in orderDocuments)
                order.RecordIssuedMiscellaneousDocuments(ElectronicMiscDocumentIssuer.DocumentEvidenceFrom(document));

            var documentedTicketServiceIds = orderTickets
                .SelectMany(ticket => ticket.Coupons)
                .Where(coupon => coupon.FinancialStatus != TicketCouponFinancialStatus.Void)
                .Select(coupon => coupon.CurrentOrderServiceId)
                .ToHashSet();

            var documentedMiscServiceIds = order.DocumentedElectronicMiscDocumentServiceIds().ToHashSet();

            var outstandingTickets = order.RequiredElectronicTicketServiceIds().Except(documentedTicketServiceIds).ToHashSet();
            var outstandingDocuments = order.RequiredElectronicMiscDocumentServiceIds().Except(documentedMiscServiceIds).ToHashSet();

            var ticketSummaries = orderTickets
                .Where(ticket => ticket.OperationId == operation.OperationId)
                .Select(ElectronicTicketIssuer.Summarize)
                .ToList();

            var documentSummaries = orderDocuments
                .Where(document => document.OperationId == operation.OperationId)
                .Select(ElectronicMiscDocumentIssuer.Summarize)
                .ToList();

            if (outstandingTickets.Count == 0 && outstandingDocuments.Count == 0)
                return await CompleteAsync(order, operation, orderTickets, orderDocuments, cancellationToken);

            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Executing,
                operation.ClaimGeneration,
                cancellationToken);

            var coverage = await _funding.VerifyCoverageAsync(
                new FundingCoverageRequest(
                    _operations.ProviderOperationKey(operation, FundingStep),
                    orderId,
                    operation.OperationId,
                    order.ObligationVersion,
                    order.Amount.GrandTotal,
                    order.CurrencyId),
                cancellationToken);

            var alreadyIrreversible = ticketSummaries.Count > 0 || documentSummaries.Count > 0;

            if (outstandingTickets.Count > 0)
            {
                var reservations = await _reservations.ListByOrderAsync(orderId, cancellationToken);
                var ticketStock = await _stocks.GetActiveForOperationAsync(
                    ownerAirlineId,
                    _ticketDocumentType,
                    operation.OperationId,
                    cancellationToken);

                var decision = IssueEligibilityPolicy.Evaluate(
                    order,
                    BuildTicketEvidence(ticketStock, coverage, reservations, documentedTicketServiceIds));

                if (!decision.IsAllowed)
                    throw ExceptionFactory.OrderOperationNotEligible(ServicingOperationKind.Issue, orderId, decision.Reasons);

                var ticketResult = await _ticketIssuer.IssueAsync(
                    new ElectronicTicketIssuanceRequest(
                        order,
                        operation,
                        ticketStock!,
                        ownerAirlineId,
                        decision.EffectiveScopeServiceIds,
                        outstandingTickets,
                        ticketSummaries),
                    cancellationToken);

                ticketSummaries = ticketResult.Summaries.ToList();
                outstandingTickets = ticketResult.Outstanding.ToHashSet();
                alreadyIrreversible = ticketResult.AlreadyIrreversible;

                if (ticketResult.Outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown)
                    return await SuspendAsync(order, operation, ticketResult.Outcome, ticketResult.Detail, ticketSummaries, documentSummaries, Outstanding(outstandingTickets, outstandingDocuments), cancellationToken);

                if (ticketResult.Outcome == ProviderOperationOutcome.Rejected)
                    return alreadyIrreversible
                        ? await ReconcileAsync(order, operation, ticketResult.Outcome, ticketResult.Detail, ticketSummaries, documentSummaries, Outstanding(outstandingTickets, outstandingDocuments), cancellationToken)
                        : await RejectAsync(order, operation, ticketResult.Detail, Outstanding(outstandingTickets, outstandingDocuments), cancellationToken);

                orderTickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);
            }

            if (outstandingDocuments.Count > 0)
            {
                var emdStock = await _stocks.GetActiveForOperationAsync(
                    ownerAirlineId,
                    _emdDocumentType,
                    operation.OperationId,
                    cancellationToken);

                var decision = ElectronicMiscDocumentEligibilityPolicy.Evaluate(
                    order,
                    new MiscellaneousDocumentIssueEvidence(
                        emdStock is not null,
                        coverage.Outcome,
                        coverage.ConfirmedAmount,
                        documentedMiscServiceIds));

                if (!decision.IsAllowed)
                    return alreadyIrreversible
                        ? await ReconcileAsync(order, operation, ProviderOperationOutcome.Rejected, decision.Reasons, ticketSummaries, documentSummaries, Outstanding(outstandingTickets, outstandingDocuments), cancellationToken)
                        : throw ExceptionFactory.OrderOperationNotEligible(ServicingOperationKind.Issue, orderId, decision.Reasons);

                var documentResult = await _miscDocumentIssuer.IssueAsync(
                    new ElectronicMiscDocumentIssuanceRequest(
                        order,
                        operation,
                        emdStock!,
                        ownerAirlineId,
                        decision.EffectiveScopeServiceIds,
                        orderTickets,
                        documentSummaries,
                        alreadyIrreversible),
                    cancellationToken);

                documentSummaries = documentResult.Summaries.ToList();
                outstandingDocuments = documentResult.Outstanding.ToHashSet();
                alreadyIrreversible = documentResult.AlreadyIrreversible;

                if (documentResult.Outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown)
                    return await SuspendAsync(order, operation, documentResult.Outcome, documentResult.Detail, ticketSummaries, documentSummaries, Outstanding(outstandingTickets, outstandingDocuments), cancellationToken);

                if (documentResult.Outcome == ProviderOperationOutcome.Rejected)
                    return alreadyIrreversible
                        ? await ReconcileAsync(order, operation, documentResult.Outcome, documentResult.Detail, ticketSummaries, documentSummaries, Outstanding(outstandingTickets, outstandingDocuments), cancellationToken)
                        : await RejectAsync(order, operation, documentResult.Detail, Outstanding(outstandingTickets, outstandingDocuments), cancellationToken);
            }

            var stillOutstanding = Outstanding(outstandingTickets, outstandingDocuments);

            if (stillOutstanding.Count > 0)
                return await ReconcileAsync(order, operation, ProviderOperationOutcome.Rejected, null, ticketSummaries, documentSummaries, stillOutstanding, cancellationToken);

            return await FinalizeAsync(order, operation, ticketSummaries, documentSummaries, cancellationToken);
        }

        private static IReadOnlyCollection<long> Outstanding(
            IReadOnlyCollection<long> tickets,
            IReadOnlyCollection<long> documents)
            => tickets.Concat(documents).Distinct().ToList();

        private async Task<IssueOrderOutcome> CompleteAsync(
            Order order,
            OrderOperation operation,
            IReadOnlyList<ElectronicTicket> orderTickets,
            IReadOnlyList<ElectronicMiscDocument> orderDocuments,
            CancellationToken cancellationToken)
        {
            var ticketSummaries = orderTickets
                .Where(ticket => ticket.OperationId == operation.OperationId)
                .Select(ElectronicTicketIssuer.Summarize)
                .ToList();

            if (ticketSummaries.Count == 0)
                ticketSummaries = orderTickets.Select(ElectronicTicketIssuer.Summarize).ToList();

            var documentSummaries = orderDocuments
                .Where(document => document.OperationId == operation.OperationId)
                .Select(ElectronicMiscDocumentIssuer.Summarize)
                .ToList();

            if (documentSummaries.Count == 0)
                documentSummaries = orderDocuments.Select(ElectronicMiscDocumentIssuer.Summarize).ToList();

            return await FinalizeAsync(order, operation, ticketSummaries, documentSummaries, cancellationToken);
        }

        private async Task<IssueOrderOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            IReadOnlyList<IssuedTicketSummary> ticketSummaries,
            IReadOnlyList<IssuedMiscellaneousDocumentSummary> documentSummaries,
            CancellationToken cancellationToken)
        {
            order.CompleteTicketing(_clock);

            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Completed,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Completed, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            return await PersistAsync(
                order,
                operation,
                ProviderOperationOutcome.Confirmed,
                ServicingOperationStatus.Completed,
                ticketSummaries,
                documentSummaries,
                [],
                null,
                cancellationToken);
        }

        private async Task<IssueOrderOutcome> SuspendAsync(
            Order order,
            OrderOperation operation,
            ProviderOperationOutcome outcome,
            string? detail,
            IReadOnlyList<IssuedTicketSummary> ticketSummaries,
            IReadOnlyList<IssuedMiscellaneousDocumentSummary> documentSummaries,
            IReadOnlyCollection<long> outstanding,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.AwaitingExternal,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(
                operation.ReceiptId,
                outcome == ProviderOperationOutcome.Unknown
                    ? CommandReceiptStatus.Unknown
                    : CommandReceiptStatus.Pending,
                cancellationToken);

            return await PersistAsync(
                order,
                operation,
                outcome,
                ServicingOperationStatus.AwaitingExternal,
                ticketSummaries,
                documentSummaries,
                outstanding,
                detail,
                cancellationToken);
        }

        private async Task<IssueOrderOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            ProviderOperationOutcome outcome,
            string? detail,
            IReadOnlyList<IssuedTicketSummary> ticketSummaries,
            IReadOnlyList<IssuedMiscellaneousDocumentSummary> documentSummaries,
            IReadOnlyCollection<long> outstanding,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.NeedsReconciliation,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.NeedsReconciliation, cancellationToken);

            return await PersistAsync(
                order,
                operation,
                outcome,
                ServicingOperationStatus.NeedsReconciliation,
                ticketSummaries,
                documentSummaries,
                outstanding,
                detail,
                cancellationToken);
        }

        private async Task<IssueOrderOutcome> RejectAsync(
            Order order,
            OrderOperation operation,
            string? detail,
            IReadOnlyCollection<long> outstanding,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Rejected,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Rejected, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            return await PersistAsync(
                order,
                operation,
                ProviderOperationOutcome.Rejected,
                ServicingOperationStatus.Rejected,
                [],
                [],
                outstanding,
                detail,
                cancellationToken);
        }

        private async Task<IssueOrderOutcome> PersistAsync(
            Order order,
            OrderOperation operation,
            ProviderOperationOutcome outcome,
            ServicingOperationStatus operationStatus,
            IReadOnlyList<IssuedTicketSummary> ticketSummaries,
            IReadOnlyList<IssuedMiscellaneousDocumentSummary> documentSummaries,
            IReadOnlyCollection<long> outstanding,
            string? detail,
            CancellationToken cancellationToken)
        {
            await _projector.ProjectAsync(order.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new IssueOrderOutcome(
                order.Id,
                operation.OperationId,
                operation.ReceiptId,
                outcome,
                operationStatus,
                order.CommercialSummary,
                order.CommercialVersion,
                ticketSummaries,
                outstanding.ToList(),
                detail,
                documentSummaries);
        }

        private static IssueEvidence BuildTicketEvidence(
            DocumentStock? stock,
            FundingCoverageResult coverage,
            IReadOnlyList<Domain.FulfillmentReservationAggregate.FulfillmentReservation> reservations,
            IReadOnlyCollection<long> documentedServiceIds)
            => new(
                stock is not null,
                coverage.Outcome,
                coverage.ConfirmedAmount,
                reservations.SelectMany(reservation => reservation.Services)
                    .Where(service => service.ObservedStatus == ReservationMemberStatus.Confirmed)
                    .Select(service => service.OrderServiceId)
                    .ToHashSet(),
                reservations.SelectMany(reservation => reservation.Services)
                    .Select(service => service.OrderServiceId)
                    .ToHashSet(),
                reservations.SelectMany(reservation => reservation.Services)
                    .Where(service => service.ObservedStatus == ReservationMemberStatus.Unknown)
                    .Select(service => service.OrderServiceId)
                    .ToHashSet(),
                documentedServiceIds);
    }
}
