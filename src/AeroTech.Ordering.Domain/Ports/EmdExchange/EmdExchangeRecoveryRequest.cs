namespace AeroTech.Ordering.Domain.Ports.EmdExchange
{
    public sealed record EmdExchangeRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string SourceDocumentNumber);
}
