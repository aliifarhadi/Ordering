using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class AncillaryExchangeConservationPolicy
    {
        public static decimal ExpectedNetCustomerCredit(
            AcceptedAddCollect? addCollect,
            AcceptedRefundDue? refundDue,
            AcceptedResidual? residual)
            => (refundDue?.Amount ?? 0m) + (residual?.Amount ?? 0m) - (addCollect?.Amount ?? 0m);

        public static void EnsureReconciles(
            IReadOnlyList<AcceptedRefundPricingLine> lines,
            AcceptedAddCollect? addCollect,
            AcceptedRefundDue? refundDue,
            AcceptedResidual? residual)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var expected = ExpectedNetCustomerCredit(addCollect, refundDue, residual);
            var actual = RefundConservationPolicy.NetCustomerCredit(lines);

            if (actual != expected)
                throw ExceptionFactory.AncillaryExchangeAmountDoesNotReconcile(expected, actual);
        }
    }
}
