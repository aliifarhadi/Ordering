using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderAirTransportServiceArgs(
        long OrderSegmentId,
        long TravellerId,
        string? Seat,
        long? AirFareId,
        string? FareBasis,
        string? FareFamilyTitle,
        long? FareNumber,
        bool IsChangeable,
        bool IsRefundable,
        bool IsUpgradable,
        Baggage? Baggage,
        Baggage? CabinBaggage);
}
