using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentRefund
{
    public interface IDocumentRefundPort
    {
        Task<DocumentRefundEligibility> CheckEligibilityAsync(
            DocumentRefundEligibilityRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentRefundResult> RefundAsync(
            DocumentRefundRequest request,
            CancellationToken cancellationToken = default);

        Task<DocumentRefundResult> RecoverAsync(
            DocumentRefundRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record DocumentRefundEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        IReadOnlyList<int> CouponNumbers);

    public sealed record DocumentRefundEligibility(EligibilityOutcome Outcome, string? Detail = null);

    public sealed record DocumentRefundRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        IReadOnlyList<int> CouponNumbers);

    public sealed record DocumentRefundRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber);

    public sealed record DocumentRefundResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? Detail = null);
}
