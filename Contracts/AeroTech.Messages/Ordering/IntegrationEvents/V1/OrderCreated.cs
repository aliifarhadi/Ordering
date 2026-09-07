using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderCreated(
        long OrderId,
        Guid UniqueIdentifierId,
        long CustomerId,
        long AirlineOfficeId,
        long CreatorUserId,
        SalesChannel Channel,
        OrderStatus Status,
        OrderType Type,
        int CurrencyId,
        int Pax,
        decimal GrandTotal,
        decimal TotalTax,
        decimal CommissionAmount,
        decimal CommissionRate,
        int OrderVersion,
        long? LinkedOrderId,
        string? LinkedPNR,
        DateTimeOffset? TimeToLive,
        DateTimeOffset CreationDate) : BaseIntegrationEvent;
}
