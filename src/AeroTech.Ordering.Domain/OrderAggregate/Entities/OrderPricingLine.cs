using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderPricingLine : Entity<long>
    {
        private readonly List<OrderPricingAllocationSet> _allocationSets = new();

        private OrderPricingLine()
        {
        }

        public OrderPricingLine(CreateOrderPricingLineArgs args)
        {
            if (args.OriginalAmount < 0m || args.SaleAmount < 0m)
                throw ExceptionFactory.PricingAmountMustBeNonNegative();

            if (args.LineRole == PricingLineRole.Reversal && args.OriginalPricingLineId is null)
                throw ExceptionFactory.ReversalRequiresOriginalLine();

            PricingComponentPolicy.EnsurePermitted(
                args.ComponentType,
                args.Effect,
                args.Direction,
                args.LineRole,
                args.Code,
                args.SettlementPartyRef,
                args.SettlementCategory);

            Id = args.Id;
            OrderId = args.OrderId;
            PriceChangeSetId = args.PriceChangeSetId;
            OrderItemId = args.OrderItemId;
            ComponentType = args.ComponentType;
            Code = args.Code;
            Description = args.Description;
            Effect = args.Effect;
            Direction = args.Direction;
            LineRole = args.LineRole;
            OriginalAmount = args.OriginalAmount;
            OriginalCurrencyId = args.OriginalCurrencyId;
            SaleAmount = args.SaleAmount;
            SaleCurrencyId = args.SaleCurrencyId;
            ExchangeRate = args.ExchangeRate;
            Refundability = args.Refundability;
            ApplicationLevel = args.ApplicationLevel;
            Quantity = args.Quantity;
            UnitOfMeasure = args.UnitOfMeasure;
            UnitPrice = args.UnitPrice;
            BasisType = args.BasisType;
            BasisReferenceId = args.BasisReferenceId;
            SourceLineRef = args.SourceLineRef;
            OccurrenceKey = args.OccurrenceKey;
            OriginalPricingLineId = args.OriginalPricingLineId;
            OriginalAllocationId = args.OriginalAllocationId;
            TransferGroupId = args.TransferGroupId;
            RelatedOperationId = args.RelatedOperationId;
            CalculationSnapshot = args.CalculationSnapshot;
            TaxDetails = args.TaxDetails;
            SettlementPartyRef = args.SettlementPartyRef;
            SettlementCategory = args.SettlementCategory;
            CreatedAt = args.CreatedAt;
        }

        public long OrderId { get; private set; }

        public long PriceChangeSetId { get; private set; }

        public long? OrderItemId { get; private set; }

        public PricingComponentType ComponentType { get; private set; }

        public string? Code { get; private set; }

        public string? Description { get; private set; }

        public PricingEffect Effect { get; private set; }

        public OrderPricingLineDirection Direction { get; private set; }

        public PricingLineRole LineRole { get; private set; }

        public decimal OriginalAmount { get; private set; }

        public int OriginalCurrencyId { get; private set; }

        public decimal SaleAmount { get; private set; }

        public int SaleCurrencyId { get; private set; }

        public ExchangeRate? ExchangeRate { get; private set; }

        public RefundabilityRule Refundability { get; private set; }

        public PricingApplicationLevel? ApplicationLevel { get; private set; }

        public decimal? Quantity { get; private set; }

        public string? UnitOfMeasure { get; private set; }

        public decimal? UnitPrice { get; private set; }

        public PricingBasisType BasisType { get; private set; }

        public long? BasisReferenceId { get; private set; }

        public string? SourceLineRef { get; private set; }

        public string? OccurrenceKey { get; private set; }

        public long? OriginalPricingLineId { get; private set; }

        public long? OriginalAllocationId { get; private set; }

        public string? TransferGroupId { get; private set; }

        public long? RelatedOperationId { get; private set; }

        public string? CalculationSnapshot { get; private set; }

        public string? TaxDetails { get; private set; }

        public string? SettlementPartyRef { get; private set; }

        public string? SettlementCategory { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public IReadOnlyCollection<OrderPricingAllocationSet> AllocationSets => _allocationSets.AsReadOnly();

        public decimal SignedSaleAmount => PricingComponentPolicy.Sign(Direction) * SaleAmount;

        public decimal SignedOriginalAmount => PricingComponentPolicy.Sign(Direction) * OriginalAmount;

        public bool AffectsCustomerBalance => Effect == PricingEffect.CustomerBalance;

        public OrderPricingAllocationSet? ActiveAllocationSet(PricingAllocationPurpose purpose)
            => _allocationSets
                .Where(set => set.Purpose == purpose)
                .OrderByDescending(set => set.Version)
                .FirstOrDefault();

        public IReadOnlyCollection<OrderPricingAllocation> CommercialAllocations()
            => ActiveAllocationSet(PricingAllocationPurpose.CommercialValue)?.Allocations ?? [];

        internal OrderPricingAllocationSet AddAllocationSet(CreateOrderPricingAllocationSetArgs args)
        {
            if (_allocationSets.Any(set => set.Purpose == args.Purpose && set.Version == args.Version))
                throw ExceptionFactory.AllocationSetVersionAlreadyExists(args.Purpose, args.Version);

            var set = new OrderPricingAllocationSet(Id, args);
            _allocationSets.Add(set);

            return set;
        }
    }
}
