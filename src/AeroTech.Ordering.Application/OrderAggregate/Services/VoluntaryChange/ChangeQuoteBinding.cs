using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange
{
    public static class ChangeQuoteBinding
    {
        public static void EnsureQuoteBindsToTheOrder(
            ChangeQuote quote,
            Order order,
            ElectronicTicket ticket,
            long orderServiceId,
            long ticketCouponId)
        {
            EnsureBinds(
                quote.OrderId,
                quote.ElectronicTicketId,
                quote.ReplacedOrderServiceId,
                quote.ReplacedTicketCouponId,
                quote.SaleCurrencyId,
                order,
                ticket,
                orderServiceId,
                ticketCouponId);
        }

        public static void EnsureAcceptedBindsToTheRequest(
            AcceptedVoluntaryChange accepted,
            Order order,
            ElectronicTicket ticket,
            long orderServiceId,
            long ticketCouponId,
            string quotedChangeId,
            int expectedCommercialVersion,
            DateTimeOffset now)
        {
            if (!string.Equals(accepted.QuotedChangeId, quotedChangeId, StringComparison.Ordinal))
                throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("quoted change identity");

            if (accepted.ExpectedCommercialVersion != expectedCommercialVersion)
                throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("commercial version");

            if (accepted.ExpiresAt <= now)
                throw ExceptionFactory.ChangeQuoteExpired(accepted.QuotedChangeId, accepted.ExpiresAt);

            if (string.IsNullOrWhiteSpace(accepted.TargetSelectionRef))
                throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("target selection");

            EnsureBinds(
                accepted.OrderId,
                accepted.ElectronicTicketId,
                accepted.ReplacedOrderServiceId,
                accepted.ReplacedTicketCouponId,
                accepted.SaleCurrencyId,
                order,
                ticket,
                orderServiceId,
                ticketCouponId);
        }

        private static void EnsureBinds(
            long quotedOrderId,
            long quotedTicketId,
            long quotedServiceId,
            long quotedCouponId,
            int quotedCurrencyId,
            Order order,
            ElectronicTicket ticket,
            long orderServiceId,
            long ticketCouponId)
        {
            if (quotedOrderId != order.Id)
                throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("order");

            if (quotedTicketId != ticket.Id)
                throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("document");

            if (quotedServiceId != orderServiceId)
                throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("order service");

            if (quotedCouponId != ticketCouponId)
                throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("ticket coupon");

            if (quotedCurrencyId != order.CurrencyId)
                throw ExceptionFactory.AcceptedChangeDoesNotMatchTheRequest("sale currency");
        }
    }
}
