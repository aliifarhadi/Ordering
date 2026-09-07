namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class OrderDetailsReadModel
    {
        public long Id { get; set; }

        public long ProjectionRevision { get; set; }

        public string SnapshotJson { get; set; } = default!;

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
