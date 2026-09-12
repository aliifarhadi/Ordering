namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
    public interface IDocumentIssuancePort
    {
        Task<DocumentIssuanceResult> IssueAsync(DocumentIssuanceRequest request, CancellationToken cancellationToken = default);

        Task<DocumentIssuanceResult> RecoverAsync(DocumentRecoveryRequest request, CancellationToken cancellationToken = default);
    }
}
