using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    internal static class LegacyPricingLineTranslation
    {
        public static OrderPricingLineDirection Direction(OrderPricingLineDirection direction)
            => direction == OrderPricingLineDirection.Debit
                ? OrderPricingLineDirection.Credit
                : OrderPricingLineDirection.Debit;

        public static OrderPricingLineCategory Category(PricingComponentType componentType)
            => componentType switch
            {
                PricingComponentType.Fare => OrderPricingLineCategory.Fare,
                PricingComponentType.ProductCharge => OrderPricingLineCategory.Ancillary,
                PricingComponentType.Tax => OrderPricingLineCategory.Tax,
                PricingComponentType.CarrierSurcharge => OrderPricingLineCategory.CarrierImposedSurcharge,
                PricingComponentType.Fee => OrderPricingLineCategory.Fee,
                PricingComponentType.Discount => OrderPricingLineCategory.Discount,
                PricingComponentType.Penalty => OrderPricingLineCategory.Penalty,
                PricingComponentType.Commission => OrderPricingLineCategory.Commission,
                _ => OrderPricingLineCategory.Charge
            };
    }
}
