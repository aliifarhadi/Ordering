using AeroTech.Framework.Core.Domain.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed class OrderLineage : ValueObject
    {
        private OrderLineage()
        {
        }

        public OrderLineage(long rootOrderId, long? parentOrderId, long? splitFromChangeId)
        {
            RootOrderId = rootOrderId;
            ParentOrderId = parentOrderId;
            SplitFromChangeId = splitFromChangeId;
        }

        public long RootOrderId { get; private set; }

        public long? ParentOrderId { get; private set; }

        public long? SplitFromChangeId { get; private set; }

        public static OrderLineage Root(long orderId) => new(orderId, null, null);

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return RootOrderId;
            yield return ParentOrderId;
            yield return SplitFromChangeId;
        }
    }
}
