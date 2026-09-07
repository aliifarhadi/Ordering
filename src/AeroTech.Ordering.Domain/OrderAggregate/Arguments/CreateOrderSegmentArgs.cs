namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderSegmentArgs(
        long Id,
        long OrderItineraryId,
        int Sequence,
        int? CabinClassId,
        long? RbdId,
        string? BookingClassCode,
        long FlightCapacityId,
        string? BookingClass,
        long AirFareId,
        long FlightId,
        int FlightVersion,
        string Number,
        int OriginAirportId,
        int? OriginAirportTerminalId,
        int DestinationAirportId,
        int? DestinationAirportTerminalId,
        int OperatingAirlineId,
        int MarketingAirlineId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime,
        int Duration,
        int AircraftId);
}
