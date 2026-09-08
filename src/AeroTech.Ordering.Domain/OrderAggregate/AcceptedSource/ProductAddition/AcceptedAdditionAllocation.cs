using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedAdditionAllocation(
        decimal SaleAmount,
        int SaleCurrencyId,
        string? ServiceRef = null,
        long? OrderTravellerId = null,
        long? OrderSegmentId = null,
        decimal? OriginalAmount = null,
        int? OriginalCurrencyId = null,
        ExchangeRate? ExchangeRate = null,
        string? CoveragePortionRef = null);
}
