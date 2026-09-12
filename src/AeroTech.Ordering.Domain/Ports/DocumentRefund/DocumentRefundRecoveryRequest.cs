namespace AeroTech.Ordering.Domain.Ports.DocumentRefund
{
    public sealed record DocumentRefundRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber);
}
