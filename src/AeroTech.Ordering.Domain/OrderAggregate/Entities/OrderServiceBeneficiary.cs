using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderServiceBeneficiary : Entity<long>
    {
        private OrderServiceBeneficiary()
        {
        }

        internal OrderServiceBeneficiary(long id, long orderServiceId, long orderTravellerId)
        {
            Id = id;
            OrderServiceId = orderServiceId;
            OrderTravellerId = orderTravellerId;
        }

        public long OrderServiceId { get; private set; }

        public long OrderTravellerId { get; private set; }
    }
}
