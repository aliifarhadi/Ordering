using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedPricingAllocationArgs(
        decimal SaleAmount,
        int SaleCurrencyId,
        long? OrderItemId = null,
        long? OrderServiceId = null,
        long? TravellerId = null,
        long? ItineraryId = null,
        long? SegmentId = null,
        string? CoveragePortionRef = null,
        decimal? OriginalAmount = null,
        int? OriginalCurrencyId = null,
        ExchangeRate? ExchangeRate = null,
        long? OriginalAllocationId = null);
}
