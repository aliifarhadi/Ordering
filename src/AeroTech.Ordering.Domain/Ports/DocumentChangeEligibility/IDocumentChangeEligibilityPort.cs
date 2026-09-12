namespace AeroTech.Ordering.Domain.Ports.DocumentChangeEligibility
{
    public interface IDocumentChangeEligibilityPort
    {
        Task<DocumentChangeEligibility> EvaluateAsync(
            DocumentChangeEligibilityRequest request,
            CancellationToken cancellationToken = default);
    }
}
