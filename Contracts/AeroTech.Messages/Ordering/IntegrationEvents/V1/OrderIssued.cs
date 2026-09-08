using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderIssued(

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

        DateTimeOffset IssuedAt,

        decimal GrandTotal,
        int CurrencyId,

        IReadOnlyList<OrderIssuedPricingLine> PricingLines
        
        ) : BaseIntegrationEvent;


        public record OrderIssuedPricingLine(

        long LineId,
      

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
