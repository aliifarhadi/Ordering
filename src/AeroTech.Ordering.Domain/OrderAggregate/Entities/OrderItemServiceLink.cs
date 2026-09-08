using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderItemServiceLink : Entity<long>
    {
        private OrderItemServiceLink()
        {
        }

        internal OrderItemServiceLink(
            long id,
            long orderId,
            long orderItemId,
            long orderServiceId,
            long linkedByChangeId,
            DateTimeOffset linkedAt)
        {
            Id = id;
            OrderId = orderId;
            OrderItemId = orderItemId;
            OrderServiceId = orderServiceId;
            LinkedByChangeId = linkedByChangeId;
            LinkedAt = linkedAt;
        }

        public long OrderId { get; private set; }

        public long OrderItemId { get; private set; }

        public long OrderServiceId { get; private set; }

        public long LinkedByChangeId { get; private set; }

        public DateTimeOffset LinkedAt { get; private set; }
    }
}
