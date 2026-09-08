using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;

namespace AeroTech.Ordering.Providers.Offer.Services
{
    public static class ServicePriceTreatmentResolver
    {
        private static readonly PricingComponentType[] PrimaryValueComponents =
        [
            PricingComponentType.Fare,
            PricingComponentType.ProductCharge
        ];

        public static ServicePriceTreatment Resolve(
            string serviceRef,
            string productRef,
            IReadOnlyList<AcceptedSourcePricingLine> acceptedLines)
        {
            if (acceptedLines.Any(line => IsPrimaryCustomerValue(line)
                                          && line.BasisType == PricingBasisType.OrderService
                                          && SameRef(line.ServiceRef, serviceRef)))
                return ServicePriceTreatment.SeparatelyPriced;

            if (acceptedLines.Any(line => IsPrimaryCustomerValue(line)
                                          && line.BasisType == PricingBasisType.OrderItem
                                          && SameRef(line.ProductRef, productRef)))
                return ServicePriceTreatment.Included;

            return ServicePriceTreatment.SupplierOpaque;
        }

        private static bool IsPrimaryCustomerValue(AcceptedSourcePricingLine line)
            => line.Effect == PricingEffect.CustomerBalance
               && line.LineRole == PricingLineRole.Original
               && PrimaryValueComponents.Contains(line.ComponentType);

        private static bool SameRef(string? left, string? right)
            => left is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
