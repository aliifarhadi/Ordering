using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Entities
{
    public sealed class FulfillmentTaskTarget : Entity<long>
    {
        private FulfillmentTaskTarget()
        {
        }

        internal FulfillmentTaskTarget(
            long id,
            long fulfillmentTaskId,
            OrderFulfillmentTargetType targetType,
            OrderFulfillmentTargetAction action,
            long? orderServiceId,
            long? orderItemId,
            string? fulfillmentReference = null,
            string? serviceReference = null)
        {
            Id = id;
            FulfillmentTaskId = fulfillmentTaskId;
            TargetType = targetType;
            Action = action;
            OrderServiceId = orderServiceId;
            OrderItemId = orderItemId;
            FulfillmentReference = fulfillmentReference;
            ServiceReference = serviceReference;
            Status = OrderFulfillmentStatus.Pending;
        }

        public long FulfillmentTaskId { get; private set; }

        public OrderFulfillmentTargetType TargetType { get; private set; }

        public OrderFulfillmentTargetAction Action { get; private set; }

        public long? OrderServiceId { get; private set; }

        public long? OrderItemId { get; private set; }

        public OrderFulfillmentStatus Status { get; private set; }

        public string? FulfillmentReference { get; private set; }

        public string? ServiceReference { get; private set; }

        internal void MarkConfirmed(string? fulfillmentReference, string? serviceReference)
        {
            FulfillmentReference = fulfillmentReference;
            ServiceReference = serviceReference;
            Status = OrderFulfillmentStatus.Confirmed;
        }

        internal void MarkStatus(OrderFulfillmentStatus status) => Status = status;
    }
}
