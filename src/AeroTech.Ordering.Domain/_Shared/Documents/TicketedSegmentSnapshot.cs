namespace AeroTech.Ordering.Domain._Shared.Documents
{
    public sealed record TicketedSegmentSnapshot(
        int MarketingAirlineId,
        string FlightNumber,
        int OriginAirportId,
        int DestinationAirportId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime,
        string? BookingClass)
    {
        public bool IsComplete
            => MarketingAirlineId > 0
               && !string.IsNullOrWhiteSpace(FlightNumber)
               && OriginAirportId > 0
               && DestinationAirportId > 0
               && DepartureDateTime != default
               && ArrivalDateTime != default;
    }
}
