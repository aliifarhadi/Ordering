using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderTransportServiceArgs(
        long Id,
        long? OrderItemId,
        long TravellerId,
        long OrderSegmentId,
        long AirFareId,
        string? FareBasis,
        string? FareFamily,
        bool IsRefundable,
        bool IsChangeable,
        Baggage CheckedBaggage,
        Baggage CabinBaggage,
        string? SeatNumber);
}
