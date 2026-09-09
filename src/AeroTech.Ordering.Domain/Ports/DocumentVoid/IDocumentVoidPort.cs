using AeroTech.Messages.Ordering.Enums;

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

    public sealed record DocumentVoidEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        AccountableDocumentKind DocumentKind,
        string DocumentNumber);

    public sealed record DocumentVoidEligibility(
        EligibilityOutcome Outcome,
        bool RefundRequiredInstead = false,
        string? Detail = null);

    public sealed record DocumentVoidRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        AccountableDocumentKind DocumentKind,
        string DocumentNumber,
        long IssuerCarrierId);

    public sealed record DocumentVoidRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        AccountableDocumentKind DocumentKind,
        string DocumentNumber);

    public sealed record DocumentVoidResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? Detail = null);
}
