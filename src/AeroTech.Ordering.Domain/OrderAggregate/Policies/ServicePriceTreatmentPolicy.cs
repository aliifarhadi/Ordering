using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Policies
{
    public static class ServicePriceTreatmentPolicy
    {
        public static readonly IReadOnlyList<PricingComponentType> PrimaryValueComponents =
        [
            PricingComponentType.Fare,
            PricingComponentType.ProductCharge
        ];

        public static bool IsPrimaryCustomerValue(
            PricingComponentType componentType,
            PricingEffect effect,
            PricingLineRole lineRole)
            => effect == PricingEffect.CustomerBalance
               && lineRole == PricingLineRole.Original
               && PrimaryValueComponents.Contains(componentType);
    }
}
