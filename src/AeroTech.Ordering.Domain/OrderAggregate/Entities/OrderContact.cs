using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderContact : Entity<long>
    {
        private readonly List<OrderContactPoint> _contactPoints = new();

        private OrderContact()
        {
        }

        public OrderContact(long id, long orderId, string? contactName)
        {
            Id = id;
            OrderId = orderId;
            ContactName = contactName;
        }

        public long OrderId { get; private set; }

        public string? ContactName { get; private set; }

        public IReadOnlyCollection<OrderContactPoint> ContactPoints => _contactPoints.AsReadOnly();

        public void AddContactPoint(OrderContactPoint contactPoint) => _contactPoints.Add(contactPoint);

        internal OrderContact CopyTo(long newId, long newOrderId, IIdGenerator idGenerator)
        {
            var copy = new OrderContact(newId, newOrderId, ContactName);

            foreach (var point in _contactPoints)
                copy.AddContactPoint(new OrderContactPoint(idGenerator.NewId(), newId, point.Type, point.Value, point.CountryCode, point.IsPrimary));

            return copy;
        }
    }
}
