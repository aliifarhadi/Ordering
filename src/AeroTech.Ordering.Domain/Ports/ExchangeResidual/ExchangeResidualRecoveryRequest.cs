namespace AeroTech.Ordering.Domain.Ports.ExchangeResidual
{
    public sealed record ExchangeResidualRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId);
}
