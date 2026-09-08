using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderAirFareConstructionItem : Entity<long>
    {
        private OrderAirFareConstructionItem()
        {
        }

        internal OrderAirFareConstructionItem(long id, long fareConstructionId, long orderItemId)
        {
            Id = id;
            FareConstructionId = fareConstructionId;
            OrderItemId = orderItemId;
        }

        public long FareConstructionId { get; private set; }

        public long OrderItemId { get; private set; }
    }
}
