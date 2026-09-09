using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class RefundPricingAuthorityPolicy
    {
        public const string Automated = "calculated";
        public const string Manual = "manual";

        public static void EnsureCalculatedAuthority(PricingSource source)
        {
            EnsureOrderingNeverPricesARefund(source);

            if (source == PricingSource.Manual)
                throw ExceptionFactory.RefundPricingAuthorityMismatch(source, Automated);
        }

        public static void EnsureManualAuthority(PricingSource source)
        {
            EnsureOrderingNeverPricesARefund(source);

            if (source != PricingSource.Manual)
                throw ExceptionFactory.RefundPricingAuthorityMismatch(source, Manual);
        }

        private static void EnsureOrderingNeverPricesARefund(PricingSource source)
        {
            if (source == PricingSource.OrderingDerived)
                throw ExceptionFactory.RefundPricingSourceNotAllowed(source);
        }
    }
}
