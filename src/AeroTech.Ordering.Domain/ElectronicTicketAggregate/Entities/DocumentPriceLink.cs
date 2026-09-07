using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class DocumentPriceLink : Entity<long>
    {
        private DocumentPriceLink()
        {
        }

        internal DocumentPriceLink(
            long id,
            long ticketId,
            long? couponId,
            long pricingLineId,
            long? allocationId,
            decimal attributedValue,
            int currencyId)
        {
            Id = id;
            TicketId = ticketId;
            CouponId = couponId;
            PricingLineId = pricingLineId;
            AllocationId = allocationId;
            AttributedValue = attributedValue;
            CurrencyId = currencyId;
        }

        public long TicketId { get; private set; }

        public long? CouponId { get; private set; }

        public long PricingLineId { get; private set; }

        public long? AllocationId { get; private set; }

        public decimal AttributedValue { get; private set; }

        public int CurrencyId { get; private set; }
    }
}
