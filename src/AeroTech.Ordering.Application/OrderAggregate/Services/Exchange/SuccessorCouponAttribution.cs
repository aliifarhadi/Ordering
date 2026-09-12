using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public static class SuccessorCouponAttribution
    {
        public static int? Reported(SuccessorDocumentIdentity? successor, AcceptedExchangePlanCoupon coupon)
            => successor?.Coupons
                .Where(identity => identity.PredecessorCouponNumber == coupon.PredecessorCouponNumber)
                .Select(identity => (int?)identity.CouponNumber)
                .FirstOrDefault();

        public static int Require(SuccessorDocumentIdentity successor, AcceptedExchangePlanCoupon coupon)
            => Reported(successor, coupon)
               ?? throw ExceptionFactory.ExchangeSuccessorAttributionUnresolved(coupon.PredecessorCouponNumber);

        public static int? Host(AcceptedExchangePlan plan, AcceptedExchangePlanCoupon coupon)
            => coupon.SuccessorCouponNumber ?? Reported(plan.Successor, coupon);
    }
}
