using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderServiceCoveredSegment : Entity<long>
    {
        private OrderServiceCoveredSegment()
        {
        }

        internal OrderServiceCoveredSegment(long id, long orderServiceId, long orderSegmentId)
        {
            Id = id;
            OrderServiceId = orderServiceId;
            OrderSegmentId = orderSegmentId;
        }

        public long OrderServiceId { get; private set; }

        public long OrderSegmentId { get; private set; }
    }
}
