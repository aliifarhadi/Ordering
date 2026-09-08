using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderFareComponentService : Entity<long>
    {
        private OrderFareComponentService()
        {
        }

        internal OrderFareComponentService(long id, long fareComponentId, long orderServiceId)
        {
            Id = id;
            FareComponentId = fareComponentId;
            OrderServiceId = orderServiceId;
        }

        public long FareComponentId { get; private set; }

        public long OrderServiceId { get; private set; }
    }
}
