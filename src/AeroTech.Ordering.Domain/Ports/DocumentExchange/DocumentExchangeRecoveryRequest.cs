namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record DocumentExchangeRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string PredecessorDocumentNumber);
}
