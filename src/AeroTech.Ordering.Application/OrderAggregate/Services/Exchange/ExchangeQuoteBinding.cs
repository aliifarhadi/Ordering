using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public static class ExchangeQuoteBinding
    {
        public static void EnsureQuoteBindsToTheOrder(ExchangeQuote quote, Order order, ExchangeScope scope)
            => EnsureBinds(
                quote.OrderId,
                quote.PredecessorElectronicTicketId,
                quote.ChangedOrderServiceIds,
                quote.Coupons,
                quote.SaleCurrencyId,
                order,
                scope);

        public static void EnsureAcceptedBindsToTheRequest(
            AcceptedExchange accepted,
            Order order,
            ExchangeScope scope,
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
                accepted.ChangedOrderServiceIds,
                accepted.Coupons,
                accepted.SaleCurrencyId,
                order,
                scope);
        }

        private static void EnsureBinds(
            long quotedOrderId,
            long quotedTicketId,
            IReadOnlyList<long> quotedChangedServiceIds,
            IReadOnlyList<AcceptedExchangeCoupon> quotedCoupons,
            int quotedCurrencyId,
            Order order,
            ExchangeScope scope)
        {
            if (quotedOrderId != order.Id)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("order");

            if (quotedTicketId != scope.PredecessorTicket.Id)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("predecessor document");

            if (quotedCurrencyId != order.CurrencyId)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("sale currency");

            if (!quotedChangedServiceIds.ToHashSet().SetEquals(scope.ChangedOrderServiceIds))
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("changed services");

            if (quotedCoupons.Count != scope.Coupons.Count)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("coupon scope");

            foreach (var coupon in scope.Coupons)
            {
                var quoted = quotedCoupons.FirstOrDefault(candidate => candidate.PredecessorTicketCouponId == coupon.TicketCouponId)
                             ?? throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("coupon scope");

                if (quoted.PredecessorCouponNumber != coupon.CouponNumber
                    || quoted.PredecessorOrderServiceId != coupon.OrderServiceId)
                    throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("coupon scope");

                if (quoted.IsReplaced != scope.ChangedOrderServiceIds.Contains(coupon.OrderServiceId))
                    throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("coupon disposition");
            }
        }
    }
}
