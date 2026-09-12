using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.ExchangeFunding
{
    public sealed record ExchangeFundingReleaseRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        string? GuaranteeReference,
        ExchangeFundingReleaseReason Reason);
}
