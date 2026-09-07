using AeroTech.Messages.FlightFlow.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderSegmentLegArgs(
        long Id,
        int Sequence,
        long LegId,
        int OriginAirportId,
        int? OriginAirportTerminalId,
        int DestinationAirportId,
        int? DestinationAirportTerminalId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime,
        FlightStopType? StopType,
        int? StopDurationAtArrivalAirport);
}
