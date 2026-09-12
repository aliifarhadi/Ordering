namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities
{
    public sealed class EmdCouponExchangeRecord
    {
        private EmdCouponExchangeRecord()
        {
        }

        public EmdCouponExchangeRecord(
            long operationId,
            long successorElectronicMiscDocumentId,
            string successorDocumentNumber,
            int successorCouponNumber,
            string decisionReference,
            string? providerReference,
            DateTimeOffset exchangedAt)
        {
            OperationId = operationId;
            SuccessorElectronicMiscDocumentId = successorElectronicMiscDocumentId;
            SuccessorDocumentNumber = successorDocumentNumber;
            SuccessorCouponNumber = successorCouponNumber;
            DecisionReference = decisionReference;
            ProviderReference = providerReference;
            ExchangedAt = exchangedAt;
        }

        public long OperationId { get; private set; }

        public long SuccessorElectronicMiscDocumentId { get; private set; }

        public string SuccessorDocumentNumber { get; private set; } = default!;

        public int SuccessorCouponNumber { get; private set; }

        public string DecisionReference { get; private set; } = default!;

        public string? ProviderReference { get; private set; }

        public DateTimeOffset ExchangedAt { get; private set; }

        public bool Names(long successorDocumentId, int successorCouponNumber)
            => SuccessorElectronicMiscDocumentId == successorDocumentId
               && SuccessorCouponNumber == successorCouponNumber;
    }
}
