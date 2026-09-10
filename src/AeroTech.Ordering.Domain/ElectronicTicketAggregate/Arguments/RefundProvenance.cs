using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments
{
    public sealed record RefundProvenance(
        string QuotedRefundId,
        PricingSource PricingSource,
        decimal ApprovedAmount,
        string ApprovedDisposition,
        string? DispositionReference = null,
        string? SourcePricingReference = null,
        string? SourceRefundType = null,
        string? SourceEvidence = null,
        ManualRefundAuthority? ManualAuthority = null);
}
