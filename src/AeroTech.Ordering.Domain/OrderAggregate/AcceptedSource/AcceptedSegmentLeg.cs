namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedSegmentLeg(
        long LegSourceId,
        int Sequence,
        int OriginAirportId,
        int? OriginAirportTerminalId,
        int DestinationAirportId,
        int? DestinationAirportTerminalId,
        DateTimeOffset DepartureAt,
        DateTimeOffset ArrivalAt);
}
