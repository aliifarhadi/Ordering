using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class AcceptedExchangePlanAncillaryReadModel
    {
        public long OperationId { get; set; }

        public long EmdCouponId { get; set; }

        public AncillaryExchangeDisposition Disposition { get; set; }

        public string? ManualReviewReason { get; set; }

        public ProviderOperationOutcome? AssociationOutcome { get; set; }

        public ProviderOperationOutcome? RefundDocumentOutcome { get; set; }

        public ProviderOperationOutcome? RefundValueOutcome { get; set; }

        public DateTimeOffset? RetentionSettledAt { get; set; }
    }
}
