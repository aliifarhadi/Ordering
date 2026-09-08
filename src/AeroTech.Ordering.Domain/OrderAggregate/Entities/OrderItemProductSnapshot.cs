using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderItemProductSnapshot : Entity<long>
    {
        private OrderItemProductSnapshot()
        {
        }

        public OrderItemProductSnapshot(long id, long orderItemId, CreateOrderItemProductSnapshotArgs args)
        {
            Id = id;
            OrderItemId = orderItemId;
            ProductType = args.ProductType;
            SourceProductReference = args.SourceProductReference;
            ProductCode = args.ProductCode;
            ProductName = args.ProductName;
            Brand = args.Brand;
            MarketingAirlineId = args.MarketingAirlineId;
            OperatingAirlineId = args.OperatingAirlineId;
            SupplierCode = args.SupplierCode;
            SourceSystem = args.SourceSystem;
            SourceOfferId = args.SourceOfferId;
            SourcePricingReference = args.SourcePricingReference;
            AcceptedAt = args.AcceptedAt;
        }

        public long OrderItemId { get; private set; }

        public ProductType ProductType { get; private set; }

        public string SourceProductReference { get; private set; } = default!;

        public string? ProductCode { get; private set; }

        public string? ProductName { get; private set; }

        public string? Brand { get; private set; }

        public int? MarketingAirlineId { get; private set; }

        public int? OperatingAirlineId { get; private set; }

        public string? SupplierCode { get; private set; }

        public string SourceSystem { get; private set; } = default!;

        public string SourceOfferId { get; private set; } = default!;

        public string? SourcePricingReference { get; private set; }

        public DateTimeOffset AcceptedAt { get; private set; }

        internal OrderItemProductSnapshot CopyTo(long newId, long newOrderItemId)
            => new(newId, newOrderItemId, new CreateOrderItemProductSnapshotArgs(
                ProductType,
                SourceProductReference,
                SourceSystem,
                SourceOfferId,
                AcceptedAt,
                ProductCode,
                ProductName,
                Brand,
                MarketingAirlineId,
                OperatingAirlineId,
                SupplierCode,
                SourcePricingReference));
    }
}
