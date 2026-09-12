namespace AeroTech.Ordering.Domain.Ports.DocumentRevalidation
{
    public sealed record DocumentRevalidationRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber);
}
