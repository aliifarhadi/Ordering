namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    public sealed class StagedRefundCorrection
    {
        internal StagedRefundCorrection(
            StagedPriceChange priceChange,
            long refundRecordId,
            IReadOnlyList<RestoredDocumentLink> restoredLinks)
        {
            PriceChange = priceChange;
            RefundRecordId = refundRecordId;
            RestoredLinks = restoredLinks;
        }

        public long RefundRecordId { get; }

        public IReadOnlyList<RestoredDocumentLink> RestoredLinks { get; }

        internal StagedPriceChange PriceChange { get; }
    }
}
