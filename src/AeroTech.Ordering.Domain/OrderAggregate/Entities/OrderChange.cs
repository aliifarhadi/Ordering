using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderChange : Entity<long>
    {
        private OrderChange()
        {
        }

        public OrderChange(CreateOrderChangeArgs args)
        {
            Id = args.Id;
            OrderId = args.OrderId;
            ChangeType = args.ChangeType;
            Reason = args.Reason;
            Source = args.Source;
            ExternalReference = args.ExternalReference;
            ActorScope = args.ActorScope;
            ActorId = args.ActorId;
            OperationId = args.OperationId;
            OccurredAt = args.OccurredAt;
        }

        public long OrderId { get; private set; }

        public OrderChangeType ChangeType { get; private set; }

        public string? Reason { get; private set; }

        public PricingSource Source { get; private set; }

        public string? ExternalReference { get; private set; }

        public string? ActorScope { get; private set; }

        public long? ActorId { get; private set; }

        public long? OperationId { get; private set; }

        public DateTimeOffset OccurredAt { get; private set; }
    }
}
