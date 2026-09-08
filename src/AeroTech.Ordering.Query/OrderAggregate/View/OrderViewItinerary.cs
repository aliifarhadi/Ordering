using AeroTech.Messages.Ordering.Enums;
using BoundDirection = AeroTech.Messages.Ordering.Enums.BoundDirection;

namespace AeroTech.Ordering.Query.OrderAggregate.View
{
    public sealed record OrderViewTraveller(
        long TravellerId,
        int Index,
        string FirstName,
        string SurName,
        AgeRange AgeRange,
        PassengerTypeCode PassengerType);

    public sealed record OrderViewJourney(
        long ItineraryId,
        int Sequence,
        long OriginAirportId,
        long DestinationAirportId,
        BoundDirection Direction,
        IReadOnlyList<OrderViewSegment> Segments);

    public sealed record OrderViewSegment(
        long SegmentId,
        int Sequence,
        string Number,
        int MarketingAirlineId,
        int OperatingAirlineId,
        int OriginAirportId,
        int DestinationAirportId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime,
        string? BookingClass);
}
