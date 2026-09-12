namespace AeroTech.Ordering.Domain.Ports.Funding
{
    public sealed record FundingCoverageRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        long ObligationVersion,
        decimal RequiredAmount,
        int CurrencyId);
}
