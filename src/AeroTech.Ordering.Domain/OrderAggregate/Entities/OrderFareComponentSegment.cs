using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderFareComponentSegment : Entity<long>
    {
        private OrderFareComponentSegment()
        {
        }

        internal OrderFareComponentSegment(long id, long fareComponentId, long orderSegmentId)
        {
            Id = id;
            FareComponentId = fareComponentId;
            OrderSegmentId = orderSegmentId;
        }

        public long FareComponentId { get; private set; }

        public long OrderSegmentId { get; private set; }
    }
}
