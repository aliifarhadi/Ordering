using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public static class ExchangeQuoteBinding
    {
        public static void EnsureQuoteBindsToTheOrder(
            ExchangeQuote quote,
            Order order,
            ElectronicTicket predecessor,
            long predecessorOrderServiceId,
            long predecessorTicketCouponId)
            => EnsureBinds(
                quote.OrderId,
                quote.PredecessorElectronicTicketId,
                quote.PredecessorOrderServiceId,
                quote.PredecessorTicketCouponId,
                quote.SaleCurrencyId,
                order,
                predecessor,
                predecessorOrderServiceId,
                predecessorTicketCouponId);

        public static void EnsureAcceptedBindsToTheRequest(
            AcceptedExchange accepted,
            Order order,
            ElectronicTicket predecessor,
            long predecessorOrderServiceId,
            long predecessorTicketCouponId,
            string quotedExchangeId,
            int expectedCommercialVersion,
            DateTimeOffset now)
        {
            if (!string.Equals(accepted.QuotedExchangeId, quotedExchangeId, StringComparison.Ordinal))
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("quoted exchange identity");

            if (accepted.ExpectedCommercialVersion != expectedCommercialVersion)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("commercial version");

            if (accepted.ExpiresAt <= now)
                throw ExceptionFactory.ExchangeQuoteExpired(accepted.QuotedExchangeId, accepted.ExpiresAt);

            if (string.IsNullOrWhiteSpace(accepted.TargetSelectionRef))
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("target selection");

            EnsureBinds(
                accepted.OrderId,
                accepted.PredecessorElectronicTicketId,
                accepted.PredecessorOrderServiceId,
                accepted.PredecessorTicketCouponId,
                accepted.SaleCurrencyId,
                order,
                predecessor,
                predecessorOrderServiceId,
                predecessorTicketCouponId);
        }

        private static void EnsureBinds(
            long quotedOrderId,
            long quotedTicketId,
            long quotedServiceId,
            long quotedCouponId,
            int quotedCurrencyId,
            Order order,
            ElectronicTicket predecessor,
            long predecessorOrderServiceId,
            long predecessorTicketCouponId)
        {
            if (quotedOrderId != order.Id)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("order");

            if (quotedTicketId != predecessor.Id)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("predecessor document");

            if (quotedServiceId != predecessorOrderServiceId)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("predecessor order service");

            if (quotedCouponId != predecessorTicketCouponId)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("predecessor ticket coupon");

            if (quotedCurrencyId != order.CurrencyId)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("sale currency");
        }
    }
}
