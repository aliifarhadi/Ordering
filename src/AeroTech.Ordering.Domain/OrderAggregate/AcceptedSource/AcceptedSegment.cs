namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedSegment(
        string SegmentRef,
        int Sequence,
        long FlightSourceId,
        int FlightVersion,
        string FlightNumber,
        int OriginAirportId,
        int? OriginAirportTerminalId,
        int DestinationAirportId,
        int? DestinationAirportTerminalId,
        int MarketingAirlineId,
        int OperatingAirlineId,
        DateTimeOffset DepartureAt,
        DateTimeOffset ArrivalAt,
        int DurationMinutes,
        int AircraftId,
        int? CabinClassId,
        long? RbdId,
        string? BookingClass,
        string? BookingClassCode,
        long CapacityReference,
        long FareReference,
        IReadOnlyList<AcceptedSegmentLeg> Legs);
}
