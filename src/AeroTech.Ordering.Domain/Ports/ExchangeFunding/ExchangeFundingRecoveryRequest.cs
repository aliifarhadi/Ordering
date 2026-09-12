namespace AeroTech.Ordering.Domain.Ports.ExchangeFunding
{
    public sealed record ExchangeFundingRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId);
}
