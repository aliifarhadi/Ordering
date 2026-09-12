namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
    public sealed record DocumentRecoveryRequest(string OperationKey, long OrderId, long OperationId, string DocumentNumber);
}
