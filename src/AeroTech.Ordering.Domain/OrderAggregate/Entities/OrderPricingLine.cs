using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderPricingLine : Entity<long>
    {
        private readonly List<OrderPricingLineAllocation> _allocations = new();

        private OrderPricingLine()
        {
        }

        public OrderPricingLine(CreateOrderPricingLineArgs args)
        {
            Id = args.Id;
            OrderId = args.OrderId;
            LineReason = args.LineReason;
            LineScope = args.LineScope;
            LineCategory = args.LineCategory;
            LineSubCategory = args.LineSubCategory;
            LineDirection = args.LineDirection;
            Code = args.Code;
            Description = args.Description;
            Reference = args.Reference;
            Amount = args.Amount;
            CurrencyId = args.CurrencyId;
            IsPercentage = args.IsPercentage;
            EquivalentAmount = args.EquivalentAmount;
            EquivalentCurrencyId = args.EquivalentCurrencyId;
            ExchangeRate = args.ExchangeRate;
            Refundability = args.Refundability;
            OriginalPricingLineId = args.OriginalPricingLineId;
        }

        public long OrderId { get; private set; }

        public OrderPricingReason LineReason { get; private set; }

        public OrderPricingLineScope LineScope { get; private set; }

        public OrderPricingLineCategory LineCategory { get; private set; }

        public OrderPricingLineSubCategory LineSubCategory { get; private set; }

        public OrderPricingLineDirection LineDirection { get; private set; }

        public string? Code { get; private set; }

        public string? Description { get; private set; }

        public string? Reference { get; private set; }

        public decimal Amount { get; private set; }

        public int CurrencyId { get; private set; }

        public bool IsPercentage { get; private set; }

        public decimal EquivalentAmount { get; private set; }

        public int EquivalentCurrencyId { get; private set; }

        public ExchangeRate? ExchangeRate { get; private set; }

        public RefundabilityRule Refundability { get; private set; }

        public long? OriginalPricingLineId { get; private set; }

        public IReadOnlyCollection<OrderPricingLineAllocation> Allocations => _allocations.AsReadOnly();

        public void AllocateTo(CreateOrderPricingLineAllocationArgs args)
            => _allocations.Add(new OrderPricingLineAllocation(Id, args));

        internal OrderPricingLine CopyTo(long newId, long newOrderId, IReadOnlyDictionary<long, long> serviceMap, IReadOnlyDictionary<long, long> itemMap, IIdGenerator idGenerator)
        {
            var copy = new OrderPricingLine(new CreateOrderPricingLineArgs(
                newId, newOrderId, LineReason, LineScope, LineCategory, LineSubCategory, LineDirection,
                Code, Description, Reference, Amount, CurrencyId, IsPercentage, EquivalentAmount, EquivalentCurrencyId,
                ExchangeRate?.Copy(), Refundability));

            foreach (var allocation in _allocations)
                copy.AllocateTo(new CreateOrderPricingLineAllocationArgs(
                    idGenerator.NewId(),
                    allocation.OrderItemId.HasValue ? itemMap[allocation.OrderItemId.Value] : (long?)null,
                    allocation.OrderServiceId.HasValue ? serviceMap[allocation.OrderServiceId.Value] : (long?)null,
                    allocation.TargetType,
                    allocation.TargetId.HasValue ? serviceMap[allocation.TargetId.Value] : (long?)null,
                    allocation.Amount, allocation.CurrencyId, allocation.EquivalentAmount, allocation.EquivalentCurrencyId,
                    allocation.ExchangeRate?.Copy()));

            return copy;
        }
    }
}
