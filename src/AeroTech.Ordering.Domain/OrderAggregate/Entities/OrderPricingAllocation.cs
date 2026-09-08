using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderPricingAllocation : Entity<long>
    {
        private OrderPricingAllocation()
        {
        }

        internal OrderPricingAllocation(long allocationSetId, CreateOrderPricingAllocationArgs args)
        {
            if (args.SaleAmount < 0m || args.OriginalAmount < 0m)
                throw ExceptionFactory.PricingAmountMustBeNonNegative();

            Id = args.Id;
            AllocationSetId = allocationSetId;
            OrderItemIdAtAllocation = args.OrderItemIdAtAllocation;
            OrderServiceId = args.OrderServiceId;
            TravellerId = args.TravellerId;
            ItineraryIdAtAllocation = args.ItineraryIdAtAllocation;
            SegmentIdAtAllocation = args.SegmentIdAtAllocation;
            CoveragePortionRef = args.CoveragePortionRef;
            OriginalAmount = args.OriginalAmount;
            OriginalCurrencyId = args.OriginalCurrencyId;
            SaleAmount = args.SaleAmount;
            SaleCurrencyId = args.SaleCurrencyId;
            ExchangeRate = args.ExchangeRate;
            OriginalAllocationId = args.OriginalAllocationId;
        }

        public long AllocationSetId { get; private set; }

        public long? OrderItemIdAtAllocation { get; private set; }

        public long? OrderServiceId { get; private set; }

        public long? TravellerId { get; private set; }

        public long? ItineraryIdAtAllocation { get; private set; }

        public long? SegmentIdAtAllocation { get; private set; }

        public string? CoveragePortionRef { get; private set; }

        public decimal? OriginalAmount { get; private set; }

        public int? OriginalCurrencyId { get; private set; }

        public decimal SaleAmount { get; private set; }

        public int SaleCurrencyId { get; private set; }

        public ExchangeRate? ExchangeRate { get; private set; }

        public long? OriginalAllocationId { get; private set; }
    }
}
