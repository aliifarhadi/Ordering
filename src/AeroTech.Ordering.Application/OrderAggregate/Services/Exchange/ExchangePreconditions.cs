using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Policies;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed class ExchangePreconditions
    {
        private readonly IElectronicTicketRepository _tickets;
        private readonly IElectronicMiscDocumentRepository _miscDocuments;

        public ExchangePreconditions(
            IElectronicTicketRepository tickets,
            IElectronicMiscDocumentRepository miscDocuments)
        {
            _tickets = tickets;
            _miscDocuments = miscDocuments;
        }

        public async Task<ExchangeScope> EnsureAsync(
            Order order,
            IReadOnlyList<long> changedOrderServiceIds,
            CancellationToken cancellationToken = default)
        {
            var changed = NormalizeChangedServices(order, changedOrderServiceIds);
            var ticket = await ResolveAccountableDocumentAsync(order.Id, changed, cancellationToken);
            var reissued = ExchangeCapabilityPolicy.ReissueScope(ticket);

            ExchangeCapabilityPolicy.EnsureEligible(
                ticket,
                reissued.Select(coupon => new ExchangeCouponScope(coupon.Id, coupon.CurrentOrderServiceId)).ToList());

            foreach (var coupon in reissued)
            {
                var service = order.RequireChangeableAirService(coupon.CurrentOrderServiceId);

                if (!service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToHashSet().SetEquals([ticket.TravelerId]))
                    throw ExceptionFactory.ExchangeTravellerMismatch(service.Id, ticket.TravelerId, ticket.DocumentNumber);
            }

            foreach (var serviceId in changed)
                order.EnsureNoActiveServiceDependsOn(serviceId);

            return new ExchangeScope(
                ticket,
                changed,
                reissued.Select(coupon => ScopeCoupon(order, coupon, changed)).ToList(),
                HistoricalContext(order, ticket),
                await AffectedAncillariesAsync(order.Id, reissued, cancellationToken));
        }

        public static IReadOnlyList<long> NormalizeChangedServices(Order order, IReadOnlyList<long>? changedOrderServiceIds)
        {
            if (changedOrderServiceIds is null
                || changedOrderServiceIds.Count == 0
                || changedOrderServiceIds.Distinct().Count() != changedOrderServiceIds.Count)
                throw ExceptionFactory.ExchangeScopeRequiresChangedServices(order.Id);

            return changedOrderServiceIds.Order().ToList();
        }

        private static ExchangeScopeCoupon ScopeCoupon(Order order, TicketCoupon coupon, IReadOnlyList<long> changed)
            => new(
                coupon.Id,
                coupon.CouponNumber,
                coupon.CurrentOrderServiceId,
                changed.Contains(coupon.CurrentOrderServiceId),
                order.SoldSegmentSnapshot(coupon.CurrentOrderServiceId)
                    ?? throw ExceptionFactory.ChangeCouponDoesNotCoverTheService(coupon.CouponNumber, coupon.CurrentOrderServiceId),
                coupon.IssuedSegment.AsTicketedSegment());

        private static IReadOnlyList<HistoricalUsedCoupon> HistoricalContext(Order order, ElectronicTicket ticket)
            => ticket.Coupons
                .Where(coupon => coupon.FinancialStatus == TicketCouponFinancialStatus.Used)
                .OrderBy(coupon => coupon.CouponNumber)
                .Select(coupon => HistoricalCoupon(order, coupon))
                .ToList();

        private static HistoricalUsedCoupon HistoricalCoupon(Order order, TicketCoupon coupon)
        {
            var issued = coupon.IssuedSegment.AsTicketedSegment();
            var bound = order.SoldSegmentSnapshot(coupon.CurrentOrderServiceId);

            return new HistoricalUsedCoupon(
                coupon.Id,
                coupon.CouponNumber,
                coupon.FinancialStatus,
                coupon.CurrentOrderServiceId,
                issued,
                bound == issued ? null : bound);
        }

        private async Task<ElectronicTicket> ResolveAccountableDocumentAsync(
            long orderId,
            IReadOnlyList<long> changed,
            CancellationToken cancellationToken)
        {
            var tickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);
            var covering = new List<ElectronicTicket>();

            foreach (var serviceId in changed)
            {
                var candidates = tickets
                    .Where(ticket => ticket.Coupons.Any(coupon => CoversForServicing(coupon, serviceId)))
                    .ToList();

                covering.Add(candidates.Count switch
                {
                    0 => throw NothingLeftToExchange(tickets, serviceId, orderId),
                    1 => candidates[0],
                    _ => throw ExceptionFactory.AccountableDocumentAmbiguous(serviceId, orderId)
                });
            }

            return covering.Select(ticket => ticket.Id).Distinct().Count() == 1
                ? covering[0]
                : throw ExceptionFactory.ExchangeScopeSpansDocuments(string.Join(", ", changed));
        }

        private static Exception NothingLeftToExchange(
            IReadOnlyList<ElectronicTicket> tickets,
            long orderServiceId,
            long orderId)
        {
            var historical = tickets
                .SelectMany(ticket => ticket.Coupons
                    .Where(coupon => coupon.CurrentOrderServiceId == orderServiceId)
                    .Select(coupon => (ticket.DocumentNumber, coupon.CouponNumber, coupon.FinancialStatus)))
                .OrderBy(candidate => candidate.CouponNumber)
                .FirstOrDefault();

            return historical.DocumentNumber is null
                ? ExceptionFactory.AccountableDocumentNotFound(orderServiceId, orderId)
                : ExceptionFactory.CouponIsNotExchangeable(
                    historical.CouponNumber, historical.DocumentNumber, historical.FinancialStatus);
        }

        private static bool CoversForServicing(TicketCoupon coupon, long orderServiceId)
            => coupon.CurrentOrderServiceId == orderServiceId
               && coupon.FinancialStatus == TicketCouponFinancialStatus.Open;

        private async Task<IReadOnlyList<AffectedAncillaryAssociation>> AffectedAncillariesAsync(
            long orderId,
            IReadOnlyList<TicketCoupon> reissued,
            CancellationToken cancellationToken)
        {
            var documents = await _miscDocuments.ListByOrderAsync(orderId, cancellationToken);
            var couponIds = reissued.Select(coupon => coupon.Id).ToHashSet();

            return documents
                .SelectMany(document => document
                    .CouponsAssociatedWith(couponIds)
                    .Select(coupon => new AffectedAncillaryAssociation(
                        document,
                        coupon,
                        reissued.Single(candidate => candidate.Id == coupon.AssociatedTicketCouponId))))
                .OrderBy(association => association.Document.DocumentNumber, StringComparer.Ordinal)
                .ThenBy(association => association.Coupon.CouponNumber)
                .ToList();
        }
    }
}
