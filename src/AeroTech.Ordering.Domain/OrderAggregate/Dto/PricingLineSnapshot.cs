using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed record PricingLineSnapshot(
        long LineId,
        long? OriginalLineId,
        decimal Amount,
        int CurrencyId,
        decimal EquivalentAmount,
        decimal? RateOfExchange,
        int? NumberOfDecimalPlaces,
        string? RateOfExchangeId,
        int? RoundingFactor,
        OrderPricingLineCategory Category,
        OrderPricingLineDirection Direction,
        string Code,
        string? Description,
        string? Reference,
        long? TrafficDocumentId,
        long? DocumentCouponId);
}
