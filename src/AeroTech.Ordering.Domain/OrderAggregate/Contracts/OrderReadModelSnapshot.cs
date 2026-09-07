using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Contracts
{
    public sealed record OrderReadModelSnapshot(
        long OrderId,
        Guid UniqueIdentifierId,
        string? RecordLocator,
        OrderStatus Status,
        OrderType Type,
        SalesChannel Channel,
        long CustomerId,
        long AirlineOfficeId,
        long CreatorUserId,
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
        DateTimeOffset CreationDate,
        DateTimeOffset OccurredAt)
    {
        public IReadOnlyList<OrderTravellerSnapshot> Travellers { get; init; } = Array.Empty<OrderTravellerSnapshot>();

        public IReadOnlyList<OrderFlightSnapshot> Flights { get; init; } = Array.Empty<OrderFlightSnapshot>();
    }

    public sealed record OrderTravellerSnapshot(
        long TravellerId,
        int Index,
        string FirstName,
        string? SurName,
        AgeRange AgeRange);

    public sealed record OrderFlightSnapshot(
        long SegmentId,
        int Sequence,
        string FlightNumber,
        int MarketingAirlineId,
        int OriginAirportId,
        int DestinationAirportId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime);
}
