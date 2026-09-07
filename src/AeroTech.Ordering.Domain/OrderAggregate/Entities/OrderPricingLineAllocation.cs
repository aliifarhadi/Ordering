using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderPricingLineAllocation : Entity<long>
    {
        private OrderPricingLineAllocation()
        {
        }

        public OrderPricingLineAllocation(long orderPricingLineId, CreateOrderPricingLineAllocationArgs args)
        {
            Id = args.Id;
            OrderPricingLineId = orderPricingLineId;
            OrderItemId = args.OrderItemId;
            OrderServiceId = args.OrderServiceId;
            TargetType = args.TargetType;
            TargetId = args.TargetId;
            Amount = args.Amount;
            CurrencyId = args.CurrencyId;
            EquivalentAmount = args.EquivalentAmount;
            EquivalentCurrencyId = args.EquivalentCurrencyId;
            ExchangeRate = args.ExchangeRate;
            OriginalAllocationId = args.OriginalAllocationId;
        }

        public long OrderPricingLineId { get; private set; }

        public long? OrderItemId { get; private set; }

        public long? OrderServiceId { get; private set; }

        public OrderPricingLineAllocationTargetType? TargetType { get; private set; }

        public long? TargetId { get; private set; }

        public decimal Amount { get; private set; }

        public int CurrencyId { get; private set; }

        public decimal EquivalentAmount { get; private set; }

        public int EquivalentCurrencyId { get; private set; }

        public ExchangeRate? ExchangeRate { get; private set; }

        public long? OriginalAllocationId { get; private set; }
    }
}
