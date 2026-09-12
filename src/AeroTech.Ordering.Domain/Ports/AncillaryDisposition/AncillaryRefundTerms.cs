using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AncillaryRefundTerms(
        decimal ApprovedAmount,
        int CurrencyId,
        string ApprovedDisposition,
        string SourceReference,
        IReadOnlyList<AcceptedRefundPricingLine> PricingLines);
}
