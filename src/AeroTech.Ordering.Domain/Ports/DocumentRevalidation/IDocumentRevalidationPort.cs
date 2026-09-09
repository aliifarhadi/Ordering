using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentRevalidation
{
    public interface IDocumentRevalidationPort
    {
        Task<DocumentRevalidationResult> RevalidateAsync(
            DocumentRevalidationRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentRevalidationResult> RecoverAsync(
            DocumentRevalidationRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record DocumentRevalidationRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long TicketCouponId,
        int CouponNumber,
        string TargetSelectionRef);

    public sealed record DocumentRevalidationRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber);

    public sealed record DocumentRevalidationResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? Detail = null);
}
