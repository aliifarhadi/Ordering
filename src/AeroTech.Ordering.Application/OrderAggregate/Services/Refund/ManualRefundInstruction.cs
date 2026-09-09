using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public sealed record ManualRefundInstruction(
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
