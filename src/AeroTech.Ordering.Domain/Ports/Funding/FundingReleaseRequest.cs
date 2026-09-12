namespace AeroTech.Ordering.Domain.Ports.Funding
{
    public sealed record FundingReleaseRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string? ExternalApplicationRef);
}
