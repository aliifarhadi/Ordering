using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Contracts;
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
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed record IssueOrderOutcome(
        long OrderId,
        long OperationId,
        ProviderOperationOutcome Outcome,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        IReadOnlyList<IssuedTicketSummary> Tickets,
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

            var alreadyIssued = await _tickets.ListByOperationAsync(operation.OperationId, cancellationToken);

            if (alreadyIssued.Count > 0)
                return Replay(order, operation, alreadyIssued);

            var reservations = await _reservations.ListByOrderAsync(orderId, cancellationToken);
            var existingTickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);

            var stock = await _stocks.GetActiveAsync(ownerAirlineId, _ticketDocumentType, cancellationToken);

            var coverage = await _funding.VerifyCoverageAsync(
                new FundingCoverageRequest(
                    _operations.ProviderOperationKey(operation, FundingStep),
                    orderId,
                    operation.OperationId,
                    order.ObligationVersion,
                    order.Amount.GrandTotal,
                    order.CurrencyId),
                cancellationToken);

            var evidence = new IssueEvidence(
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
                existingTickets.SelectMany(ticket => ticket.Coupons)
                    .Select(coupon => coupon.CurrentOrderServiceId)
                    .ToHashSet());

            var decision = IssueEligibilityPolicy.Evaluate(order, evidence);

            if (!decision.IsAllowed)
                throw ExceptionFactory.OrderOperationNotEligible(ServicingOperationKind.Issue, orderId, decision.Reasons);

            var plans = BuildPlans(order, decision.EffectiveScopeServiceIds);
            var issuedDocuments = new List<IssuedServiceDocument>();
            var summaries = new List<IssuedTicketSummary>();

            foreach (var plan in plans)
            {
                var allocation = stock!.Allocate(operation.OperationId, $"Ticket:{plan.TravelerId}", _idGenerator, _clock);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var result = await _documents.IssueAsync(
                    new DocumentIssuanceRequest(
                        _operations.ProviderOperationKey(operation, $"{IssueStep}:{plan.TravelerId}"),
                        orderId,
                        operation.OperationId,
                        plan.TravelerId,
                        allocation.DocumentNumber,
                        ownerAirlineId is > int.MaxValue ? 0 : (int)ownerAirlineId,
                        order.CurrencyId,
                        plan.Coupons.Sum(coupon => coupon.IssuanceValue),
                        plan.Coupons
                            .Select(coupon => new DocumentCouponRequest(coupon.OrderServiceId, coupon.JourneySegmentId, coupon.IssuanceValue))
                            .ToList()),
                    cancellationToken);

                if (result.Outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown)
                {
                    await _projector.ProjectAsync(orderId, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    return new IssueOrderOutcome(
                        orderId,
                        operation.OperationId,
                        result.Outcome,
                        order.CommercialSummary,
                        order.CommercialVersion,
                        summaries,
                        result.Detail);
                }

                if (result.Outcome == ProviderOperationOutcome.Rejected)
                {
                    stock.Retire(operation.OperationId, $"Ticket:{plan.TravelerId}", _clock);
                    await _operations.ResolveAsync(orderId, operation, cancellationToken);
                    await _projector.ProjectAsync(orderId, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    return new IssueOrderOutcome(
                        orderId,
                        operation.OperationId,
                        ProviderOperationOutcome.Rejected,
                        order.CommercialSummary,
                        order.CommercialVersion,
                        summaries,
                        result.Detail);
                }

                var ticket = ElectronicTicket.Issue(
                    _idGenerator.NewId(),
                    orderId,
                    plan.TravelerId,
                    operation.OperationId,
                    allocation.DocumentNumber,
                    ownerAirlineId is > int.MaxValue ? 0 : (int)ownerAirlineId,
                    order.AirlineOfficeId,
                    DocumentAuthority.Local,
                    null,
                    order.CurrencyId,
                    plan.Coupons,
                    _idGenerator,
                    _clock);

                ticket.RecordProviderConfirmation(result.ProviderReference, _clock);

                await _tickets.AddAsync(ticket, cancellationToken);
                stock.MarkIssued(operation.OperationId, $"Ticket:{plan.TravelerId}", _clock);

                foreach (var coupon in ticket.Coupons)
                    issuedDocuments.Add(new IssuedServiceDocument(coupon.OrderServiceId, ticket.Id, coupon.Id));

                summaries.Add(new IssuedTicketSummary(ticket.Id, plan.TravelerId, ticket.DocumentNumber, ticket.Coupons.Count));
            }

            order.ApplyIssuedDocuments(issuedDocuments, _clock);

            await _operations.ResolveAsync(orderId, operation, cancellationToken);
            await _projector.ProjectAsync(orderId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new IssueOrderOutcome(
                orderId,
                operation.OperationId,
                ProviderOperationOutcome.Confirmed,
                order.CommercialSummary,
                order.CommercialVersion,
                summaries,
                null);
        }

        private IssueOrderOutcome Replay(
            Order order,
            OrderOperation operation,
            IReadOnlyList<ElectronicTicket> tickets)
            => new(
                order.Id,
                operation.OperationId,
                ProviderOperationOutcome.Confirmed,
                order.CommercialSummary,
                order.CommercialVersion,
                tickets
                    .Select(ticket => new IssuedTicketSummary(ticket.Id, ticket.TravelerId, ticket.DocumentNumber, ticket.Coupons.Count))
                    .ToList(),
                null);

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
