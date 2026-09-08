using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record PricingLineSnapshot(
        long LineId,
        long PriceChangeSetId,
        long? OriginalLineId,
        decimal OriginalAmount,
        int OriginalCurrencyId,
        decimal SaleAmount,
        int SaleCurrencyId,
        decimal? RateOfExchange,
        int? NumberOfDecimalPlaces,
        string? RateOfExchangeId,
        int? RoundingFactor,
        PricingComponentType ComponentType,
        PricingEffect Effect,
        OrderPricingLineDirection Direction,
        PricingLineRole LineRole,
        string Code,
        string? Description,
        string? SourceLineRef,
        long? TrafficDocumentId,
        long? DocumentCouponId);
}
