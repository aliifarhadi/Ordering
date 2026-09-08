using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderServiceCoveredService : Entity<long>
    {
        private OrderServiceCoveredService()
        {
        }

        internal OrderServiceCoveredService(long id, long orderServiceId, long coveredOrderServiceId)
        {
            Id = id;
            OrderServiceId = orderServiceId;
            CoveredOrderServiceId = coveredOrderServiceId;
        }

        public long OrderServiceId { get; private set; }

        public long CoveredOrderServiceId { get; private set; }
    }
}
