using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.RefundValue
{
    public interface IRefundValuePort
    {
        Task<RefundValueResult> RequestAsync(RefundValueRequest request, CancellationToken cancellationToken = default);

        Task<RefundValueRecovery> RecoverAsync(
            RefundValueRecoveryRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed record RefundValueRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        decimal ApprovedAmount,
        int CurrencyId,
        string ApprovedDisposition,
        string? DispositionReference);

    public sealed record RefundValueRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId);

    public sealed record RefundValueResult(
        ProviderOperationOutcome Outcome,
        string? ValueMovementReference = null,
        string? Detail = null);

    public sealed record RefundValueRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ValueMovementReference = null,
        string? Detail = null);
}
