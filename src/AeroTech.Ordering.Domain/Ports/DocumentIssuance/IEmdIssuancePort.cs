namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
    public interface IEmdIssuancePort
    {
        Task<DocumentIssuanceResult> IssueAsync(EmdIssuanceRequest request, CancellationToken cancellationToken = default);

        Task<DocumentIssuanceResult> RecoverAsync(DocumentRecoveryRequest request, CancellationToken cancellationToken = default);
    }
}
