using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund
{
    public sealed record RefundQuote(
        string SourceSystem,
        string QuotedRefundId,
        PricingSource PricingSource,
        long OrderId,
        int ExpectedCommercialVersion,
        long ElectronicTicketId,
        int SaleCurrencyId,
        IReadOnlyList<long> TicketCouponIds,
        IReadOnlyList<AcceptedRefundPricingLine> PricingLines,
        decimal ApprovedRefundAmount,
        string ApprovedDisposition,
        DateTimeOffset ExpiresAt,
        string? SourcePricingReference = null);
}
