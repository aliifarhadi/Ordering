namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    internal sealed class AcceptedSourceRefMap
    {
        public Dictionary<string, long> SegmentIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> SegmentJourneyRefs { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, long> TravellerIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, int> TravellerIndexes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, long> ProductItemIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, long> ServiceIds { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
