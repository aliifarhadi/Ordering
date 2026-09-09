using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class RefundConservationPolicy
    {
        public static decimal NetCustomerCredit(IReadOnlyList<AcceptedRefundPricingLine> lines)
            => -lines
                .Where(line => line.Effect == PricingEffect.CustomerBalance)
                .Sum(line => PricingComponentPolicy.Sign(line.Direction) * line.SaleAmount);

        public static void EnsureReconciles(
            IReadOnlyList<AcceptedRefundPricingLine> lines,
            decimal approvedRefundAmount)
        {
            var netCustomerCredit = NetCustomerCredit(lines);

            if (netCustomerCredit != approvedRefundAmount)
                throw ExceptionFactory.RefundAmountDoesNotReconcile(approvedRefundAmount, netCustomerCredit);
        }
    }
}
