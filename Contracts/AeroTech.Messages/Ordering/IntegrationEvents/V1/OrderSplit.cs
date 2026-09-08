using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderSplit(

        long OrderId,
        long AirlineOfficeId,

        string? RecordLocator,

        Guid UniqueIdentifierId,
        int CommercialVersion,
        int EventOrdinal,
        OrderStatus Status,
        OrderType Type,
        SalesChannel Channel,

        long CustomerId,

        DateTimeOffset SplitAt,

        IReadOnlyList<long> MovedTravellerIds,

        bool WasTicketed,

        decimal GrandTotal,
        int CurrencyId,

        IReadOnlyList<OrderSplitPricingLine> PricingLines,

        OrderSplitNewOrder NewOrder

        ) : BaseIntegrationEvent;

    public record OrderSplitNewOrder(

        long OrderId,

        string? RecordLocator,

        Guid UniqueIdentifierId,
        int CommercialVersion,

        decimal GrandTotal,

        IReadOnlyList<OrderSplitPricingLine> PricingLines);

    public record OrderSplitPricingLine(

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
