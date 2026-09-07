using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderCancelled(

        long OrderId,
        long AirlineOfficeId,

        string? RecordLocator,

        Guid UniqueIdentifierId,
        int Version,
        OrderStatus Status,
        OrderType Type,
        SalesChannel Channel,

        long CustomerId,

        VoidReason Reason,
        long? CancelledBy,
        DateTimeOffset CancelledAt,
        bool WasTicketed,
        decimal GrandTotal,
        int CurrencyId,

        IReadOnlyList<OrderCancelledPricingLine> PricingLines
        
        ) : BaseIntegrationEvent;

 public record OrderCancelledPricingLine(

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

