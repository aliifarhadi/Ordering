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

        public IReadOnlyCollection<OrderPricingAllocation> Allocations => _allocations.AsReadOnly();

        internal void Allocate(CreateOrderPricingAllocationArgs args)
            => _allocations.Add(new OrderPricingAllocation(Id, args));

        internal void EnsureReconciles(decimal parentSaleAmount, int parentSaleCurrencyId)
        {
            if (_allocations.Any(allocation => allocation.SaleCurrencyId != parentSaleCurrencyId))
                throw ExceptionFactory.AllocationCurrencyMismatch();

            var allocated = _allocations.Sum(allocation => allocation.SaleAmount);

            switch (Completeness)
            {
                case PricingAllocationCompleteness.Complete when allocated != parentSaleAmount:
                    throw ExceptionFactory.AllocationSetDoesNotReconcile(allocated, parentSaleAmount);

                case PricingAllocationCompleteness.Partial when allocated > parentSaleAmount:
                    throw ExceptionFactory.AllocationSetExceedsParent(allocated, parentSaleAmount);

                case PricingAllocationCompleteness.Unavailable when _allocations.Count > 0:
                    throw ExceptionFactory.UnavailableAllocationSetMustBeEmpty();
            }
        }
    }
}
