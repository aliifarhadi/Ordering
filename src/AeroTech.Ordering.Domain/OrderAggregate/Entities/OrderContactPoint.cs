using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderContactPoint : Entity<long>
    {
        private OrderContactPoint()
        {
        }

        public OrderContactPoint(long id, long orderContactId, ContactPointType type, string value, string? countryCode, bool isPrimary)
        {
            Id = id;
            OrderContactId = orderContactId;
            Type = type;
            Value = value;
            CountryCode = countryCode;
            IsPrimary = isPrimary;
        }

        public long OrderContactId { get; private set; }

        public ContactPointType Type { get; private set; }

        public string Value { get; private set; } = default!;

        public string? CountryCode { get; private set; }

        public bool IsPrimary { get; private set; }
    }
}
