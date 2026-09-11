using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanAncillaryRow
    {
        public long OperationId { get; set; }

        public long ElectronicMiscDocumentId { get; set; }

        public string EmdDocumentNumber { get; set; } = null!;

        public int EmdCouponNumber { get; set; }

        public long EmdCouponId { get; set; }

        public long PredecessorTicketCouponId { get; set; }

        public string PredecessorDocumentNumber { get; set; } = null!;

        public int PredecessorCouponNumber { get; set; }

        public AncillaryExchangeDisposition Disposition { get; set; }

        public int? TargetPredecessorCouponNumber { get; set; }

        public long? TargetSuccessorTicketCouponId { get; set; }

        public string DecisionReference { get; set; } = null!;

        public int DecisionVersion { get; set; }

        public ProviderOperationOutcome? AssociationOutcome { get; set; }

        public string? AssociationProviderReference { get; set; }

        public string? AssociationDetail { get; set; }
    }
}
