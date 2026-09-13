using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Reconciliation
{
    public sealed record ServicingExternalEvidence(
        long OperationId,
        ServicingEvidenceStage Stage,
        ProviderOperationOutcome Outcome,
        string? ProviderReference,
        string? Detail,
        AccountableDocumentKind? DocumentKind,
        string? DocumentNumber,
        DateTimeOffset RecordedAt)
    {
        public bool IsConfirmed => Outcome == ProviderOperationOutcome.Confirmed;

        public bool IsUnresolved
            => Outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown;
    }
}
