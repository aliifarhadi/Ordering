using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderGenericServiceDetail : Entity<long>
    {
        private OrderGenericServiceDetail()
        {
        }

        internal OrderGenericServiceDetail(
            long id,
            long orderServiceId,
            string schemaName,
            string schemaVersion,
            string attributesJson)
        {
            Id = id;
            OrderServiceId = orderServiceId;
            SchemaName = schemaName;
            SchemaVersion = schemaVersion;
            AttributesJson = attributesJson;
        }

        public long OrderServiceId { get; private set; }

        public string SchemaName { get; private set; } = default!;

        public string SchemaVersion { get; private set; } = default!;

        public string AttributesJson { get; private set; } = default!;
    }
}
