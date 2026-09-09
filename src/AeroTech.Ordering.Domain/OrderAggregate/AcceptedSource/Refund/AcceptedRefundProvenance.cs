using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund
{
    public sealed record AcceptedRefundProvenance(
        string QuotedRefundId,
        PricingSource PricingSource,
        decimal ApprovedAmount,
        string ApprovedDisposition,
        string? DispositionReference = null,
        string? SourcePricingReference = null,
        string? SourceRefundType = null,
        string? SourceEvidence = null,
        ManualRefundAuthority? ManualAuthority = null)
    {
        public static AcceptedRefundProvenance Of(AcceptedRefund accepted, ManualRefundAuthority? manualAuthority)
            => new(
                accepted.QuotedRefundId,
                accepted.PricingSource,
                accepted.ApprovedRefundAmount,
                accepted.ApprovedDisposition,
                accepted.DispositionReference,
                accepted.SourcePricingReference,
                accepted.SourceRefundType,
                accepted.SourceEvidence,
                manualAuthority);
    }
}
