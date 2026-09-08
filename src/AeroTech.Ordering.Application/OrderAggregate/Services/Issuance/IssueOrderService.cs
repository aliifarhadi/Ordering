using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Contracts;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Domain.Ports.Funding;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record IssueOrderOutcome(
        long OrderId,
        long OperationId,
        long ReceiptId,
        ProviderOperationOutcome Outcome,
        ServicingOperationStatus OperationStatus,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        IReadOnlyList<IssuedTicketSummary> Tickets,
        IReadOnlyList<long> OutstandingServiceIds,
        string? Detail);

    public sealed record IssuedTicketSummary(long TicketId, long TravelerId, string DocumentNumber, int CouponCount);

    public interface IIssueOrderService
    {
        Task<IssueOrderOutcome> IssueAsync(
            long orderId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }

    public sealed class IssueOrderService : IIssueOrderService
    {
        public const string FundingStep = "coverage";
        public const string IssueStep = "issue";

        private readonly IOrderRepository _orders;
        private readonly IFulfillmentReservationRepository _reservations;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IDocumentStockRepository _stocks;
        private readonly IFundingCoveragePort _funding;
        private readonly IDocumentIssuancePort _documents;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly IHomeOperatorProvider _homeOperator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;
        private readonly string _ticketDocumentType;

        public IssueOrderService(
            IOrderRepository orders,
            IFulfillmentReservationRepository reservations,
            IElectronicTicketRepository tickets,
            IDocumentStockRepository stocks,
            IFundingCoveragePort funding,
            IDocumentIssuancePort documents,
            IOrderOperationCoordinator operations,
            IServicingOperationStore operationStore,
            ICommandReceiptStore receipts,
            IHomeOperatorProvider homeOperator,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector,
            IOptions<OrderOperationOptions> options)
        {
            _orders = orders;
            _reservations = reservations;
            _tickets = tickets;
            _stocks = stocks;
            _funding = funding;
            _documents = documents;
            _operations = operations;
            _operationStore = operationStore;
            _receipts = receipts;
            _homeOperator = homeOperator;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;

            _ticketDocumentType = options.Value.TicketDocumentType
                ?? throw new InvalidOperationException(
                    $"'{OrderOperationOptions.SectionName}:{nameof(OrderOperationOptions.TicketDocumentType)}' must be configured.");
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

            order.RecordIssuedDocuments(DocumentEvidenceFrom(orderTickets));

            var requiredServiceIds = order.OrderServices
                .Where(service => service.RequiresDocument && service.Status != OrderServiceStatus.Cancelled)
                .Select(service => service.Id)
                .ToHashSet();

            var documentedServiceIds = orderTickets
                .SelectMany(ticket => ticket.Coupons)
                .Where(coupon => coupon.FinancialStatus != TicketCouponFinancialStatus.Void)
                .Select(coupon => coupon.CurrentOrderServiceId)
                .ToHashSet();

            var outstanding = requiredServiceIds.Except(documentedServiceIds).ToHashSet();

            if (outstanding.Count == 0)
                return await CompleteAsync(order, operation, orderTickets, cancellationToken);

            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Executing,
                operation.ClaimGeneration,
                cancellationToken);

            var reservations = await _reservations.ListByOrderAsync(orderId, cancellationToken);
            var stock = await _stocks.GetActiveForOperationAsync(
                ownerAirlineId,
                _ticketDocumentType,
                operation.OperationId,
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

            var decision = IssueEligibilityPolicy.Evaluate(
                order,
                BuildEvidence(stock, coverage, reservations, documentedServiceIds));

            if (!decision.IsAllowed)
                throw ExceptionFactory.OrderOperationNotEligible(ServicingOperationKind.Issue, orderId, decision.Reasons);

            var plans = BuildPlans(order, decision.EffectiveScopeServiceIds);
            var summaries = orderTickets
                .Where(ticket => ticket.OperationId == operation.OperationId)
                .Select(Summarize)
                .ToList();

            var alreadyIrreversible = summaries.Count > 0;

            foreach (var plan in plans)
            {
                var role = DocumentRole(plan.TravelerId);
                var priorAttempt = stock!.FindAllocation(operation.OperationId, role);

                DocumentIssuanceResult result;
                DocumentStockAllocation allocation;

                if (priorAttempt is { State: StockNumberState.Reserved })
                {
                    allocation = priorAttempt;

                    result = await _documents.RecoverAsync(
                        new DocumentRecoveryRequest(
                            _operations.ProviderOperationKey(operation, $"{IssueStep}:{plan.TravelerId}"),
                            orderId,
                            operation.OperationId,
                            allocation.DocumentNumber),
                        cancellationToken);
                }
                else
                {
                    allocation = stock.Allocate(operation.OperationId, role, _idGenerator, _clock);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    result = await _documents.IssueAsync(
                        BuildIssuanceRequest(order, operation, plan, allocation, ownerAirlineId),
                        cancellationToken);
                }

                if (result.Outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown)
                    return await SuspendAsync(order, operation, result, summaries, outstanding, cancellationToken);

                if (result.Outcome == ProviderOperationOutcome.Rejected)
                {
                    if (alreadyIrreversible)
                        return await ReconcileAsync(order, operation, result, summaries, outstanding, cancellationToken);

                    stock.Retire(operation.OperationId, role, _clock);

                    return await RejectAsync(order, operation, result, outstanding, cancellationToken);
                }

                var ticket = ElectronicTicket.Issue(
                    _idGenerator.NewId(),
                    orderId,
                    plan.TravelerId,
                    operation.OperationId,
                    allocation.DocumentNumber,
                    ownerAirlineId,
                    order.AirlineOfficeId,
                    DocumentAuthority.Local,
                    null,
                    order.CurrencyId,
                    plan.Coupons,
                    _idGenerator,
                    _clock);

                ticket.RecordProviderConfirmation(result.ProviderReference, _clock);

                await _tickets.AddAsync(ticket, cancellationToken);
                stock.MarkIssued(operation.OperationId, role, _clock);

                var evidence = DocumentEvidenceFrom([ticket]);
                order.RecordIssuedDocuments(evidence);

                foreach (var document in evidence)
                    outstanding.Remove(document.OrderServiceId);

                summaries.Add(Summarize(ticket));
                alreadyIrreversible = true;
            }

            if (outstanding.Count > 0)
                return await ReconcileAsync(order, operation, null, summaries, outstanding, cancellationToken);

            return await FinalizeAsync(order, operation, summaries, cancellationToken);
        }

        private async Task<IssueOrderOutcome> CompleteAsync(
            Order order,
            OrderOperation operation,
            IReadOnlyList<ElectronicTicket> orderTickets,
            CancellationToken cancellationToken)
        {
            var summaries = orderTickets
                .Where(ticket => ticket.OperationId == operation.OperationId)
                .Select(Summarize)
                .ToList();

            if (summaries.Count == 0)
                summaries = orderTickets.Select(Summarize).ToList();

            return await FinalizeAsync(order, operation, summaries, cancellationToken);
        }

        private async Task<IssueOrderOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            IReadOnlyList<IssuedTicketSummary> summaries,
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
                summaries,
                [],
                null,
                cancellationToken);
        }

        private async Task<IssueOrderOutcome> SuspendAsync(
            Order order,
            OrderOperation operation,
            DocumentIssuanceResult result,
            IReadOnlyList<IssuedTicketSummary> summaries,
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
                result.Outcome == ProviderOperationOutcome.Unknown
                    ? CommandReceiptStatus.Unknown
                    : CommandReceiptStatus.Pending,
                cancellationToken);

            return await PersistAsync(
                order,
                operation,
                result.Outcome,
                ServicingOperationStatus.AwaitingExternal,
                summaries,
                outstanding,
                result.Detail,
                cancellationToken);
        }

        private async Task<IssueOrderOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            DocumentIssuanceResult? result,
            IReadOnlyList<IssuedTicketSummary> summaries,
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
                result?.Outcome ?? ProviderOperationOutcome.Rejected,
                ServicingOperationStatus.NeedsReconciliation,
                summaries,
                outstanding,
                result?.Detail,
                cancellationToken);
        }

        private async Task<IssueOrderOutcome> RejectAsync(
            Order order,
            OrderOperation operation,
            DocumentIssuanceResult result,
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
                outstanding,
                result.Detail,
                cancellationToken);
        }

        private async Task<IssueOrderOutcome> PersistAsync(
            Order order,
            OrderOperation operation,
            ProviderOperationOutcome outcome,
            ServicingOperationStatus operationStatus,
            IReadOnlyList<IssuedTicketSummary> summaries,
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
                summaries,
                outstanding.ToList(),
                detail);
        }

        private DocumentIssuanceRequest BuildIssuanceRequest(
            Order order,
            OrderOperation operation,
            TicketPlan plan,
            DocumentStockAllocation allocation,
            long ownerAirlineId)
            => new(
                _operations.ProviderOperationKey(operation, $"{IssueStep}:{plan.TravelerId}"),
                order.Id,
                operation.OperationId,
                plan.TravelerId,
                allocation.DocumentNumber,
                ownerAirlineId,
                order.CurrencyId,
                plan.Coupons.Sum(coupon => coupon.IssuanceValue),
                plan.Coupons
                    .Select(coupon => new DocumentCouponRequest(coupon.OrderServiceId, coupon.JourneySegmentId, coupon.IssuanceValue))
                    .ToList());

        private static IssueEvidence BuildEvidence(
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

        private static IReadOnlyCollection<IssuedServiceDocument> DocumentEvidenceFrom(IEnumerable<ElectronicTicket> tickets)
            => tickets
                .SelectMany(ticket => ticket.Coupons
                    .Where(coupon => coupon.FinancialStatus != TicketCouponFinancialStatus.Void)
                    .Select(coupon => new IssuedServiceDocument(coupon.CurrentOrderServiceId, ticket.Id, coupon.Id)))
                .ToList();

        private static IssuedTicketSummary Summarize(ElectronicTicket ticket)
            => new(ticket.Id, ticket.TravelerId, ticket.DocumentNumber, ticket.Coupons.Count);

        private static string DocumentRole(long travelerId) => $"Ticket:{travelerId}";

        private static IReadOnlyList<TicketPlan> BuildPlans(Order order, IReadOnlyList<long> scope)
            => order.OrderServices
                .OfType<OrderAirTransportService>()
                .Where(service => scope.Contains(service.Id))
                .GroupBy(service => service.TravellerId)
                .OrderBy(group => group.Key)
                .Select(group => new TicketPlan(
                    group.Key,
                    group
                        .OrderBy(service => order.Segments.Single(segment => segment.Id == service.OrderSegmentId).Sequence)
                        .Select(service => BuildCoupon(order, service))
                        .ToList()))
                .ToList();

        private static TicketCouponIssuance BuildCoupon(Order order, OrderAirTransportService service)
        {
            var segment = order.Segments.Single(candidate => candidate.Id == service.OrderSegmentId);

            var allocations = order.PricingLines
                .SelectMany(line => line.Allocations
                    .Where(allocation => allocation.OrderServiceId == service.Id)
                    .Select(allocation => new
                    {
                        PricingLineId = line.Id,
                        AllocationId = allocation.Id,
                        allocation.EquivalentAmount
                    }))
                .ToList();

            return new TicketCouponIssuance(
                service.Id,
                segment.Id,
                new IssuedSegmentSnapshot(
                    segment.MarketingAirlineId,
                    segment.Number,
                    segment.OriginAirportId,
                    segment.DestinationAirportId,
                    segment.DepartureDateTime,
                    segment.ArrivalDateTime,
                    segment.BookingClass),
                service.FareBasis,
                allocations.Sum(allocation => allocation.EquivalentAmount),
                allocations
                    .Select(allocation => new TicketCouponPriceLink(
                        allocation.PricingLineId,
                        allocation.AllocationId,
                        allocation.EquivalentAmount))
                    .ToList());
        }

        private sealed record TicketPlan(long TravelerId, IReadOnlyList<TicketCouponIssuance> Coupons);
    }
}
