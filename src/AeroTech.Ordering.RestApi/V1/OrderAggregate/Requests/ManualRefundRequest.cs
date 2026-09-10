using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record ManualRefundRequest(
        string AuthorityReference,
        string Reason,
        decimal ApprovedRefundAmount,
        string ApprovedDisposition,
        IReadOnlyList<AcceptedRefundPricingLine> PricingLines,
        string? DispositionReference = null,
        string? SourcePricingReference = null,
        string? SourceRefundType = null,
        string? SourceEvidence = null);
}
