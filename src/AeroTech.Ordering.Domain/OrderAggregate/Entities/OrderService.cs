using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public abstract class OrderService : Entity<long>
    {
        protected OrderService()
        {
        }

        protected OrderService(CreateOrderServiceArgs args)
        {
            Id = args.Id;
            OrderId = args.OrderId;
            OrderItemId = args.OrderItemId;
            ServiceType = args.ServiceType;
            ServiceCode = args.ServiceCode;
            Name = args.Name;
            Status = OrderServiceStatus.Pending;
            CommercialStatus = OrderServiceCommercialStatus.Pending;
            FulfillmentStatus = OrderFulfillmentStatus.Pending;
            DeliveryStatus = OrderServiceDeliveryStatus.NotReady;
            FinancialStatus = OrderServiceFinancialStatus.Priced;
            DocumentStatus = OrderServiceDocumentStatus.Pending;
            DeliveryModel = args.DeliveryModel;
            RequiresFulfillment = args.RequiresFulfillment;
            RequiresSupplierConfirmation = args.RequiresSupplierConfirmation;
            RequiresDocument = args.RequiresDocument;
            ProviderType = args.ProviderType;
            SupplierCode = args.SupplierCode;
            CreatedAt = args.CreatedAt;
        }

        public long OrderId { get; private set; }

        public long OrderItemId { get; private set; }

        public OrderServiceType ServiceType { get; private set; }

        public string ServiceCode { get; private set; } = default!;

        public string Name { get; private set; } = default!;

        public OrderServiceStatus Status { get; private set; }

        public OrderServiceCommercialStatus CommercialStatus { get; private set; }

        public OrderFulfillmentStatus FulfillmentStatus { get; private set; }

        public OrderServiceDeliveryStatus DeliveryStatus { get; private set; }

        public OrderServiceFinancialStatus FinancialStatus { get; private set; }

        public OrderServiceDocumentStatus DocumentStatus { get; private set; }

        public DeliveryModel DeliveryModel { get; private set; }

        public bool RequiresFulfillment { get; private set; }

        public bool RequiresSupplierConfirmation { get; private set; }

        public bool RequiresDocument { get; private set; }

        public OrderProviderType ProviderType { get; private set; }

        public string? SupplierCode { get; private set; }

        public string? HoldBatchId { get; private set; }

        public string? SeatHoldReference { get; private set; }

        public long? TrafficDocumentId { get; private set; }

        public long? DocumentCouponId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public long? ElectronicTicketId { get; private set; }

        public long? TicketCouponId { get; private set; }

        internal void Activate()
        {
            if (Status == OrderServiceStatus.Pending)
            {
                Status = OrderServiceStatus.Active;
                CommercialStatus = OrderServiceCommercialStatus.Active;
            }
        }

        internal void MarkReservationConfirmed()
        {
            FulfillmentStatus = OrderFulfillmentStatus.Confirmed;

            if (Status is OrderServiceStatus.Pending or OrderServiceStatus.Active)
                Status = OrderServiceStatus.Fulfilled;
        }

        internal void MarkReservationReleased()
        {
            FulfillmentStatus = OrderFulfillmentStatus.Cancelled;
            HoldBatchId = null;
            SeatHoldReference = null;
        }

        internal void MarkDocumented(long electronicTicketId, long ticketCouponId)
        {
            DocumentStatus = OrderServiceDocumentStatus.Issued;
            ElectronicTicketId = electronicTicketId;
            TicketCouponId = ticketCouponId;
        }

        internal void MarkFulfilled(string? holdBatchId, string? seatHoldReference)
        {
            FulfillmentStatus = OrderFulfillmentStatus.Confirmed;
            HoldBatchId = holdBatchId;
            SeatHoldReference = seatHoldReference;
        }

        internal void MarkIssued(long trafficDocumentId, long documentCouponId)
        {
            DocumentStatus = OrderServiceDocumentStatus.Issued;
            TrafficDocumentId = trafficDocumentId;
            DocumentCouponId = documentCouponId;
        }

        internal void MarkVoided()
        {
            DocumentStatus = OrderServiceDocumentStatus.Voided;
            Status = OrderServiceStatus.Cancelled;
            CommercialStatus = OrderServiceCommercialStatus.Cancelled;
            DeliveryStatus = OrderServiceDeliveryStatus.Unused;
            FinancialStatus = OrderServiceFinancialStatus.Refunded;
        }

        internal void MarkCancelled()
        {
            if (DocumentStatus == OrderServiceDocumentStatus.Issued)
                DocumentStatus = OrderServiceDocumentStatus.Cancelled;

            Status = OrderServiceStatus.Cancelled;
            CommercialStatus = OrderServiceCommercialStatus.Cancelled;
            DeliveryStatus = OrderServiceDeliveryStatus.Unused;
            FinancialStatus = OrderServiceFinancialStatus.Refunded;
        }

        internal void CopyStateFrom(OrderService source, string? holdBatchId)
        {
            Status = source.Status;
            CommercialStatus = source.CommercialStatus;
            FulfillmentStatus = source.FulfillmentStatus;
            DeliveryStatus = source.DeliveryStatus;
            FinancialStatus = source.FinancialStatus;
            DocumentStatus = source.DocumentStatus;
            HoldBatchId = holdBatchId;
            SeatHoldReference = source.SeatHoldReference;
            TrafficDocumentId = source.TrafficDocumentId;
            DocumentCouponId = source.DocumentCouponId;
        }
    }
}
