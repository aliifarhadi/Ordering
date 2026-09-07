using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class OrderReadModel
    {
        public long Id { get; set; }

        public Guid UniqueIdentifierId { get; set; }

        public string? RecordLocator { get; set; }

        public OrderStatus Status { get; set; }

        public OrderType Type { get; set; }

        public SalesChannel Channel { get; set; }

        public long CustomerId { get; set; }

        public long AirlineOfficeId { get; set; }

        public long CreatorUserId { get; set; }

        public int CurrencyId { get; set; }

        public int Pax { get; set; }

        public decimal GrandTotal { get; set; }

        public decimal TotalTax { get; set; }

        public decimal CommissionAmount { get; set; }

        public decimal CommissionRate { get; set; }

        public int CommercialVersion { get; set; }

        public long? LinkedOrderId { get; set; }

        public string? LinkedPNR { get; set; }

        public DateTimeOffset? TimeToLive { get; set; }

        public DateTimeOffset CreationDate { get; set; }

        public DateTimeOffset LastProjectedAt { get; set; }
    }
}
