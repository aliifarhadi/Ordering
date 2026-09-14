namespace AeroTech.Ordering.Domain.Servicing.Reconciliation
{
    public sealed record ServicingEvidenceRecording(
        ServicingExternalEvidence Attempted,
        ServicingExternalEvidence Durable,
        bool Applied);
}
