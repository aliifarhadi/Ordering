using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities
{
    public sealed class EmdPriceLink : Entity<long>
    {
        private EmdPriceLink()
        {
        }

        internal EmdPriceLink(
            long id,
            long electronicMiscDocumentId,
            long emdCouponId,
            long pricingLineId,
            long? allocationId,
            decimal attributedValue,
            int currencyId)
        {
            Id = id;
            ElectronicMiscDocumentId = electronicMiscDocumentId;
            EmdCouponId = emdCouponId;
            PricingLineId = pricingLineId;
            AllocationId = allocationId;
            AttributedValue = attributedValue;
            CurrencyId = currencyId;
        }

        public long ElectronicMiscDocumentId { get; private set; }

        public long EmdCouponId { get; private set; }

        public long PricingLineId { get; private set; }

        public long? AllocationId { get; private set; }

        public decimal AttributedValue { get; private set; }

        public int CurrencyId { get; private set; }
    }
}
