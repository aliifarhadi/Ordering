using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderPricingAllocationSet : Entity<long>
    {
        private readonly List<OrderPricingAllocation> _allocations = new();

        private OrderPricingAllocationSet()
        {
        }

        internal OrderPricingAllocationSet(long orderPricingLineId, CreateOrderPricingAllocationSetArgs args)
        {
            if (args.Method != PricingAllocationMethod.SourceProvided
                && args.Method != PricingAllocationMethod.DirectBasis
                && string.IsNullOrWhiteSpace(args.PolicyVersion))
                throw ExceptionFactory.DerivedAllocationRequiresMethodEvidence(args.Method);

            Id = args.Id;
            OrderPricingLineId = orderPricingLineId;
            OrderIdAtCreation = args.OrderIdAtCreation;
            Purpose = args.Purpose;
            Version = args.Version;
            SupersedesAllocationSetId = args.SupersedesAllocationSetId;
            Source = args.Source;
            Method = args.Method;
            Completeness = args.Completeness;
            PricingContextRef = args.PricingContextRef;
            PolicyVersion = args.PolicyVersion;
            CreatedAt = args.CreatedAt;
        }

        public long OrderPricingLineId { get; private set; }

        public long OrderIdAtCreation { get; private set; }

        public PricingAllocationPurpose Purpose { get; private set; }

        public int Version { get; private set; }

        public long? SupersedesAllocationSetId { get; private set; }

        public PricingSource Source { get; private set; }

        public PricingAllocationMethod Method { get; private set; }

        public PricingAllocationCompleteness Completeness { get; private set; }

        public string? PricingContextRef { get; private set; }

        public string? PolicyVersion { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public decimal ResidualSaleAmount { get; private set; }

        public int ResidualSaleCurrencyId { get; private set; }

        public decimal? ResidualOriginalAmount { get; private set; }

        public int? ResidualOriginalCurrencyId { get; private set; }

        public IReadOnlyCollection<OrderPricingAllocation> Allocations => _allocations.AsReadOnly();

        public bool IsFullyAttributed => ResidualSaleAmount == 0m;

        internal void Allocate(CreateOrderPricingAllocationArgs args)
            => _allocations.Add(new OrderPricingAllocation(Id, args));

        internal void EnsureReconciles(
            decimal parentSaleAmount,
            int parentSaleCurrencyId,
            decimal parentOriginalAmount,
            int parentOriginalCurrencyId)
        {
            if (_allocations.Any(allocation => allocation.SaleCurrencyId != parentSaleCurrencyId))
                throw ExceptionFactory.AllocationCurrencyMismatch();

            var allocatedSale = _allocations.Sum(allocation => allocation.SaleAmount);

            switch (Completeness)
            {
                case PricingAllocationCompleteness.Complete when allocatedSale != parentSaleAmount:
                    throw ExceptionFactory.AllocationSetDoesNotReconcile(allocatedSale, parentSaleAmount);

                case PricingAllocationCompleteness.Partial when allocatedSale > parentSaleAmount:
                    throw ExceptionFactory.AllocationSetExceedsParent(allocatedSale, parentSaleAmount);

                case PricingAllocationCompleteness.Unavailable when _allocations.Count > 0:
                    throw ExceptionFactory.UnavailableAllocationSetMustBeEmpty();
            }

            ResidualSaleAmount = parentSaleAmount - allocatedSale;
            ResidualSaleCurrencyId = parentSaleCurrencyId;

            ReconcileOriginalValues(parentOriginalAmount, parentOriginalCurrencyId);
        }

        private void ReconcileOriginalValues(decimal parentOriginalAmount, int parentOriginalCurrencyId)
        {
            var supplying = _allocations.Where(allocation => allocation.OriginalAmount.HasValue).ToList();

            if (supplying.Count == 0)
            {
                ResidualOriginalAmount = null;
                ResidualOriginalCurrencyId = null;

                return;
            }

            if (supplying.Count != _allocations.Count)
                throw ExceptionFactory.AllocationOriginalValueIncomplete();

            if (supplying.Any(allocation => allocation.OriginalCurrencyId != parentOriginalCurrencyId))
                throw ExceptionFactory.AllocationCurrencyMismatch();

            var allocatedOriginal = supplying.Sum(allocation => allocation.OriginalAmount!.Value);

            switch (Completeness)
            {
                case PricingAllocationCompleteness.Complete when allocatedOriginal != parentOriginalAmount:
                    throw ExceptionFactory.AllocationSetDoesNotReconcile(allocatedOriginal, parentOriginalAmount);

                case PricingAllocationCompleteness.Partial when allocatedOriginal > parentOriginalAmount:
                    throw ExceptionFactory.AllocationSetExceedsParent(allocatedOriginal, parentOriginalAmount);
            }

            ResidualOriginalAmount = parentOriginalAmount - allocatedOriginal;
            ResidualOriginalCurrencyId = parentOriginalCurrencyId;
        }
    }
}
