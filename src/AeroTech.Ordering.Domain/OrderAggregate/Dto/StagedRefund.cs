namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed class StagedRefund
    {
        internal StagedRefund(
            StagedPriceChange priceChange,
            IReadOnlyList<long> serviceIds,
            decimal approvedRefundAmount)
        {
            PriceChange = priceChange;
            ServiceIds = serviceIds;
            ApprovedRefundAmount = approvedRefundAmount;
        }

        public IReadOnlyList<long> ServiceIds { get; }

        public decimal ApprovedRefundAmount { get; }

        internal StagedPriceChange PriceChange { get; }
    }
}
