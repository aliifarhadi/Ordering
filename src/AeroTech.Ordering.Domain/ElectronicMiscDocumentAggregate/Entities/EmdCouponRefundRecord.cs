namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities
{
    public sealed class EmdCouponRefundRecord
    {
        private EmdCouponRefundRecord()
        {
        }

        internal EmdCouponRefundRecord(
            long operationId,
            decimal approvedAmount,
            int currencyId,
            string approvedDisposition,
            string decisionReference,
            string? providerReference,
            DateTimeOffset refundedAt)
        {
            OperationId = operationId;
            ApprovedAmount = approvedAmount;
            CurrencyId = currencyId;
            ApprovedDisposition = approvedDisposition;
            DecisionReference = decisionReference;
            ProviderReference = providerReference;
            RefundedAt = refundedAt;
        }

        public long OperationId { get; private set; }

        public decimal ApprovedAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public string ApprovedDisposition { get; private set; } = default!;

        public string DecisionReference { get; private set; } = default!;

        public string? ProviderReference { get; private set; }

        public DateTimeOffset RefundedAt { get; private set; }
    }
}
