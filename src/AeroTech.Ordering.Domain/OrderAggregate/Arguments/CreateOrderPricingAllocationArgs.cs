using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderPricingAllocationArgs(
        long Id,
        decimal SaleAmount,
        int SaleCurrencyId,
        long? OrderItemIdAtAllocation = null,
        long? OrderServiceId = null,
        long? TravellerId = null,
        long? ItineraryIdAtAllocation = null,
        long? SegmentIdAtAllocation = null,
        string? CoveragePortionRef = null,
        decimal? OriginalAmount = null,
        int? OriginalCurrencyId = null,
        ExchangeRate? ExchangeRate = null,
        long? OriginalAllocationId = null);
}
