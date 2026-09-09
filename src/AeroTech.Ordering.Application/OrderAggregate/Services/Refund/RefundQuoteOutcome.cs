using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public sealed record RefundQuoteOutcome(
        long OrderId,
        int CommercialVersion,
        long ElectronicTicketId,
        string DocumentNumber,
        string QuotedRefundId,
        string SourceSystem,
        PricingSource PricingSource,
        decimal ApprovedRefundAmount,
        int SaleCurrencyId,
        string ApprovedDisposition,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<long> TicketCouponIds,
        IReadOnlyList<AcceptedRefundPricingLine> PricingLines,
        string? SourcePricingReference);
}
