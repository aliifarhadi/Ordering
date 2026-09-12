namespace AeroTech.Ordering.Domain.Ports.DocumentRevalidation
{
    public interface IDocumentRevalidationPort
    {
        Task<DocumentRevalidationResult> RevalidateAsync(
            DocumentRevalidationRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentRevalidationRecovery> RecoverAsync(
            DocumentRevalidationRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
