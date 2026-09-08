using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record ElectronicTicketIssuanceRequest(
        Order Order,
        OrderOperation Operation,
        DocumentStock Stock,
        long OwnerAirlineId,
        IReadOnlyList<long> Scope,
        IReadOnlyCollection<long> Outstanding,
        IReadOnlyList<IssuedTicketSummary> AlreadyIssued);

    public sealed record ElectronicTicketIssuanceOutcome(
        ProviderOperationOutcome Outcome,
        IReadOnlyList<IssuedTicketSummary> Summaries,
        IReadOnlyCollection<long> Outstanding,
        bool AlreadyIrreversible,
        string? Detail);

    public interface IElectronicTicketIssuer
    {
        Task<ElectronicTicketIssuanceOutcome> IssueAsync(
            ElectronicTicketIssuanceRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed class ElectronicTicketIssuer : IElectronicTicketIssuer
    {
        public const string IssueStep = "issue";

        private readonly IElectronicTicketRepository _tickets;
        private readonly IDocumentIssuancePort _documents;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;

        public ElectronicTicketIssuer(
            IElectronicTicketRepository tickets,
            IDocumentIssuancePort documents,
            IOrderOperationCoordinator operations,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock)
        {
            _tickets = tickets;
            _documents = documents;
            _operations = operations;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
        }

        public async Task<ElectronicTicketIssuanceOutcome> IssueAsync(
            ElectronicTicketIssuanceRequest request,
            CancellationToken cancellationToken = default)
        {
            var order = request.Order;
            var operation = request.Operation;
            var stock = request.Stock;

            var summaries = request.AlreadyIssued.ToList();
            var outstanding = request.Outstanding.ToHashSet();
            var alreadyIrreversible = summaries.Count > 0;

            foreach (var plan in BuildPlans(order, request.Scope))
            {
                var role = DocumentRole(plan.TravelerId);
                var priorAttempt = stock.FindAllocation(operation.OperationId, role);

                DocumentIssuanceResult result;
                DocumentStockAllocation allocation;

                if (priorAttempt is { State: StockNumberState.Reserved })
                {
                    allocation = priorAttempt;

                    result = await _documents.RecoverAsync(
                        new DocumentRecoveryRequest(
                            _operations.ProviderOperationKey(operation, $"{IssueStep}:{plan.TravelerId}"),
                            order.Id,
                            operation.OperationId,
                            allocation.DocumentNumber),
                        cancellationToken);
                }
                else
                {
                    allocation = stock.Allocate(operation.OperationId, role, _idGenerator, _clock);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    result = await _documents.IssueAsync(
                        BuildIssuanceRequest(order, operation, plan, allocation, request.OwnerAirlineId),
                        cancellationToken);
                }

                if (result.Outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown)
                    return new ElectronicTicketIssuanceOutcome(result.Outcome, summaries, outstanding, alreadyIrreversible, result.Detail);

                if (result.Outcome == ProviderOperationOutcome.Rejected)
                {
                    if (!alreadyIrreversible)
                        stock.Retire(operation.OperationId, role, _clock);

                    return new ElectronicTicketIssuanceOutcome(result.Outcome, summaries, outstanding, alreadyIrreversible, result.Detail);
                }

                var ticket = ElectronicTicket.Issue(
                    _idGenerator.NewId(),
                    order.Id,
                    plan.TravelerId,
                    operation.OperationId,
                    allocation.DocumentNumber,
                    request.OwnerAirlineId,
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

            return new ElectronicTicketIssuanceOutcome(
                ProviderOperationOutcome.Confirmed,
                summaries,
                outstanding,
                alreadyIrreversible,
                null);
        }

        public static IReadOnlyCollection<IssuedServiceDocument> DocumentEvidenceFrom(IEnumerable<ElectronicTicket> tickets)
            => tickets
                .SelectMany(ticket => ticket.Coupons
                    .Where(coupon => coupon.FinancialStatus != TicketCouponFinancialStatus.Void)
                    .Select(coupon => new IssuedServiceDocument(coupon.CurrentOrderServiceId, ticket.Id, coupon.Id)))
                .ToList();

        public static IssuedTicketSummary Summarize(ElectronicTicket ticket)
            => new(ticket.Id, ticket.TravelerId, ticket.DocumentNumber, ticket.Coupons.Count);

        private static string DocumentRole(long travelerId) => $"Ticket:{travelerId}";

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

        private static IReadOnlyList<TicketPlan> BuildPlans(Order order, IReadOnlyList<long> scope)
            => order.OrderServices
                .Where(service => service.IsAirTransport && scope.Contains(service.Id))
                .GroupBy(service => service.SoleBeneficiaryId)
                .OrderBy(group => group.Key)
                .Select(group => new TicketPlan(
                    group.Key,
                    group
                        .OrderBy(service => order.Segments.Single(segment => segment.Id == service.SoldSegmentId!.Value).Sequence)
                        .Select(service => BuildCoupon(order, service))
                        .ToList()))
                .ToList();

        private static TicketCouponIssuance BuildCoupon(Order order, OrderService service)
        {
            var segment = order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value);

            var allocations = order.ServiceValueAttributions(service.Id)
                .Select(attribution => new
                {
                    attribution.PricingLineId,
                    attribution.AllocationId,
                    EquivalentAmount = attribution.SignedSaleAmount
                })
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
                order.ResolveIssueFareBasis(service.Id),
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
