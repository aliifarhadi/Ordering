using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderDocumentVoided(

        long DocumentId,
        string DocumentNumber,

        long OrderId,
        long AirlineOfficeId,
        string? RecordLocator,
        Guid UniqueIdentifierId,
        int CommercialVersion,
        OrderStatus Status,
        OrderType Type,
        SalesChannel Channel,
        decimal GrandTotal,
        int CurrencyId,

        long CustomerId,

        VoidReason Reason,
        long VoidedBy,
        DateTimeOffset VoidedAt,
        decimal ReversedAmount,

        IReadOnlyList<OrderDocumentVoidedPricingLine> PricingLines) : BaseIntegrationEvent;

    public record OrderDocumentVoidedPricingLine(

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

        long? DocumentCouponId);
}
