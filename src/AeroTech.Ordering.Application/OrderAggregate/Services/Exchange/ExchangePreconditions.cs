using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
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
            long predecessorOrderServiceId,
            CancellationToken cancellationToken = default)
        {
            var service = order.RequireChangeableAirService(predecessorOrderServiceId);
            var (ticket, coupon) = await ResolveAccountableDocumentAsync(order.Id, service.Id, cancellationToken);

            ticket.EnsureCanBeExchanged(coupon.Id, service.Id);
            order.EnsureNoActiveServiceDependsOn(service.Id);

            await EnsureNoAssociatedMiscDocumentAsync(order.Id, coupon, cancellationToken);

            return new ExchangeScope(service, ticket, coupon);
        }

        private async Task<(ElectronicTicket Ticket, TicketCoupon Coupon)> ResolveAccountableDocumentAsync(
            long orderId,
            long orderServiceId,
            CancellationToken cancellationToken)
        {
            var tickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);

            var candidates = tickets
                .SelectMany(ticket => ticket.Coupons
                    .Where(coupon => coupon.CurrentOrderServiceId == orderServiceId)
                    .Select(coupon => (Ticket: ticket, Coupon: coupon)))
                .ToList();

            return candidates.Count switch
            {
                0 => throw ExceptionFactory.AccountableDocumentNotFound(orderServiceId, orderId),
                1 => candidates[0],
                _ => throw ExceptionFactory.AccountableDocumentAmbiguous(orderServiceId, orderId)
            };
        }

        private async Task EnsureNoAssociatedMiscDocumentAsync(
            long orderId,
            TicketCoupon coupon,
            CancellationToken cancellationToken)
        {
            var documents = await _miscDocuments.ListByOrderAsync(orderId, cancellationToken);

            var associated = documents.FirstOrDefault(document =>
                document.StatusSummary != ElectronicMiscDocumentStatus.Voided
                && document.Coupons.Any(emdCoupon => emdCoupon.AssociatedTicketCouponId == coupon.Id));

            if (associated is not null)
                throw ExceptionFactory.ExchangeBlockedByAssociatedMiscDocument(
                    coupon.CouponNumber, associated.DocumentNumber);
        }
    }
}
