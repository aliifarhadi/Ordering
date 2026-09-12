namespace AeroTech.Ordering.Domain.Ports.RefundValue
{
    public sealed record RefundValueRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId);
}
