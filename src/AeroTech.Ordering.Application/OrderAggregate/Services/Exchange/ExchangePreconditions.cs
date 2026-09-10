using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
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
            var coupons = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            ticket.EnsureCanBeExchanged(coupons
                .Select(coupon => new ExchangeCouponScope(coupon.Id, coupon.CurrentOrderServiceId))
                .ToList());

            foreach (var coupon in coupons)
            {
                var service = order.RequireChangeableAirService(coupon.CurrentOrderServiceId);

                if (!service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToHashSet().SetEquals([ticket.TravelerId]))
                    throw ExceptionFactory.ExchangeTravellerMismatch(service.Id, ticket.TravelerId, ticket.DocumentNumber);
            }

            foreach (var serviceId in changed)
                order.EnsureNoActiveServiceDependsOn(serviceId);

            await EnsureNoAssociatedMiscDocumentAsync(order.Id, coupons, cancellationToken);

            return new ExchangeScope(
                ticket,
                changed,
                coupons.Select(coupon => new PredecessorCouponEvidence(coupon.Id, coupon.CouponNumber, coupon.CurrentOrderServiceId)).ToList());
        }

        public static IReadOnlyList<long> NormalizeChangedServices(Order order, IReadOnlyList<long>? changedOrderServiceIds)
        {
            if (changedOrderServiceIds is null
                || changedOrderServiceIds.Count == 0
                || changedOrderServiceIds.Distinct().Count() != changedOrderServiceIds.Count)
                throw ExceptionFactory.ExchangeScopeRequiresChangedServices(order.Id);

            return changedOrderServiceIds.Order().ToList();
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
                    .Where(ticket => ticket.Coupons.Any(coupon => coupon.CurrentOrderServiceId == serviceId))
                    .ToList();

                covering.Add(candidates.Count switch
                {
                    0 => throw ExceptionFactory.AccountableDocumentNotFound(serviceId, orderId),
                    1 => candidates[0],
                    _ => throw ExceptionFactory.AccountableDocumentAmbiguous(serviceId, orderId)
                });
            }

            return covering.Select(ticket => ticket.Id).Distinct().Count() == 1
                ? covering[0]
                : throw ExceptionFactory.ExchangeScopeSpansDocuments(string.Join(", ", changed));
        }

        private async Task EnsureNoAssociatedMiscDocumentAsync(
            long orderId,
            IReadOnlyList<TicketCoupon> coupons,
            CancellationToken cancellationToken)
        {
            var documents = await _miscDocuments.ListByOrderAsync(orderId, cancellationToken);
            var couponIds = coupons.Select(coupon => coupon.Id).ToHashSet();

            foreach (var document in documents.Where(document => document.StatusSummary != ElectronicMiscDocumentStatus.Voided))
            {
                var association = document.Coupons.FirstOrDefault(emdCoupon =>
                    emdCoupon.AssociatedTicketCouponId is { } associated && couponIds.Contains(associated));

                if (association is not null)
                    throw ExceptionFactory.ExchangeBlockedByAssociatedMiscDocument(
                        coupons.Single(coupon => coupon.Id == association.AssociatedTicketCouponId).CouponNumber,
                        document.DocumentNumber);
            }
        }
    }
}
