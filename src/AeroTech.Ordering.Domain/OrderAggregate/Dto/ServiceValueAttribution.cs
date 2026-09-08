using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record ServiceValueAttribution(
        long PricingLineId,
        PricingComponentType ComponentType,
        PricingEffect Effect,
        decimal SignedSaleAmount,
        int SaleCurrencyId,
        long? AllocationId);
}
