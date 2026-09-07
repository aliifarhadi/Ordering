using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderExternalReference : Entity<long>
    {
        private OrderExternalReference()
        {
        }

        internal OrderExternalReference(
            long id,
            long orderId,
            ExternalReferenceType type,
            string sourceSystem,
            string reference,
            DateTimeOffset recordedAt)
        {
            Id = id;
            OrderId = orderId;
            Type = type;
            SourceSystem = sourceSystem;
            Reference = reference;
            RecordedAt = recordedAt;
        }

        public long OrderId { get; private set; }

        public ExternalReferenceType Type { get; private set; }

        public string SourceSystem { get; private set; } = default!;

        public string Reference { get; private set; } = default!;

        public DateTimeOffset RecordedAt { get; private set; }
    }
}
