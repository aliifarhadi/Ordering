using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain._Shared.Documents
{
    public sealed class DocumentVoidRecord
    {
        private DocumentVoidRecord()
        {
        }

        internal DocumentVoidRecord(
            long operationId,
            VoidReason reason,
            string? reasonDetail,
            long voidedBy,
            DateTimeOffset voidedAt,
            string? providerReference)
        {
            OperationId = operationId;
            Reason = reason;
            ReasonDetail = string.IsNullOrWhiteSpace(reasonDetail) ? null : reasonDetail.Trim();
            VoidedBy = voidedBy;
            VoidedAt = voidedAt;
            ProviderReference = providerReference;
        }

        public long OperationId { get; private set; }

        public VoidReason Reason { get; private set; }

        public string? ReasonDetail { get; private set; }

        public long VoidedBy { get; private set; }

        public DateTimeOffset VoidedAt { get; private set; }

        public string? ProviderReference { get; private set; }
    }
}
