namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed class StagedExchange
    {
        internal StagedExchange(
            StagedPriceChange priceChange,
            IReadOnlyList<StagedExchangeCoupon> coupons,
            long successorElectronicTicketId,
            IReadOnlyDictionary<string, long> pricingLineIdsBySourceRef)
        {
            PriceChange = priceChange;
            Coupons = coupons;
            SuccessorElectronicTicketId = successorElectronicTicketId;
            PricingLineIdsBySourceRef = pricingLineIdsBySourceRef;
        }

        public IReadOnlyList<StagedExchangeCoupon> Coupons { get; }

        public long SuccessorElectronicTicketId { get; }

        public IReadOnlyDictionary<string, long> PricingLineIdsBySourceRef { get; }

        internal StagedPriceChange PriceChange { get; }
    }
}
