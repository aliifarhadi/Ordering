using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedJourney(
        string JourneyRef,
        int Sequence,
        long OriginAirportId,
        long DestinationAirportId,
        BoundDirection Direction,
        IReadOnlyList<AcceptedSegment> Segments);
}
