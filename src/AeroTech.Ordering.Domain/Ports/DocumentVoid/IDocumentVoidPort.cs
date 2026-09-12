namespace AeroTech.Ordering.Domain.Ports.DocumentVoid
{
    public interface IDocumentVoidPort
    {
        Task<DocumentVoidEligibility> CheckEligibilityAsync(
            DocumentVoidEligibilityRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentVoidResult> VoidAsync(
            DocumentVoidRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentVoidResult> RecoverAsync(
            DocumentVoidRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }
}
