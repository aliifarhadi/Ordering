using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Dto
{
    public sealed record OrderDto(
        long Id,
        string? RecordLocator,
        OrderStatus Status,
        SalesChannel Channel,
        long CustomerId,
        long AirlineOfficeId,
        int CurrencyId,
        int Pax,
        decimal GrandTotal,
        decimal TotalTax,
        DateTimeOffset CreationDate);
}
