using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderItem : Entity<long>
    {
        

        private OrderItem()
        {
        }

        public OrderItem(CreateOrderItemArgs args, OrderItemPolicySnapshot policySnapshot)
        {
            Id = args.Id;
            OrderId = args.OrderId;
            ProductType = args.ProductType;
            ProductCode = args.ProductCode;
            ProductName = args.ProductName;
            Quantity = args.Quantity;
            UnitOfMeasure = args.UnitOfMeasure;
            CommercialStatus = OrderItemCommercialStatus.Active;
            PaymentStatus = OrderItemPaymentStatus.Unpaid;
            FulfillmentStatus = OrderItemFulfillmentStatus.NotFulfilled;
            FinancialStatus = OrderItemFinancialStatus.None;
            PolicySnapshot = policySnapshot;
            CreationDate = args.CreationDate;
        }

        public long OrderId { get; private set; }

        public ProductType ProductType { get; private set; }

        public string ProductCode { get; private set; } = default!;

        public string ProductName { get; private set; } = default!;

        public decimal Quantity { get; private set; }

        public OrderItemUnitOfMeasure UnitOfMeasure { get; private set; }

        public OrderItemCommercialStatus CommercialStatus { get; private set; }

        public OrderItemPaymentStatus PaymentStatus { get; private set; }

        public OrderItemFulfillmentStatus FulfillmentStatus { get; private set; }

        public OrderItemFinancialStatus FinancialStatus { get; private set; }

        public OrderItemPolicySnapshot PolicySnapshot { get; private set; } = default!;

        public DateTimeOffset CreationDate { get; private set; }

        internal void MarkCancelled()
        {
            CommercialStatus = OrderItemCommercialStatus.Cancelled;
            PaymentStatus = OrderItemPaymentStatus.Refunded;
            FinancialStatus = OrderItemFinancialStatus.Refunded;
        }

        internal OrderItem CopyTo(long newId, long newOrderId, IIdGenerator idGenerator)
        {
            var copy = new OrderItem(
                new CreateOrderItemArgs(newId, newOrderId, ProductType, ProductCode, ProductName, Quantity, UnitOfMeasure, CreationDate),
                PolicySnapshot.CopyTo(idGenerator.NewId(), newId));

            copy.CommercialStatus = CommercialStatus;
            copy.PaymentStatus = PaymentStatus;
            copy.FulfillmentStatus = FulfillmentStatus;
            copy.FinancialStatus = FinancialStatus;

            return copy;
        }
    }
}
