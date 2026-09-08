using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderFarePricingGroupTraveller : Entity<long>
    {
        private OrderFarePricingGroupTraveller()
        {
        }

        internal OrderFarePricingGroupTraveller(long id, long pricingGroupId, long orderTravellerId)
        {
            Id = id;
            PricingGroupId = pricingGroupId;
            OrderTravellerId = orderTravellerId;
        }

        public long PricingGroupId { get; private set; }

        public long OrderTravellerId { get; private set; }
    }
}
