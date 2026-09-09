using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Domain.Ports.Refund
{
    public interface IRefundQuotePort
    {
        Task<RefundQuote> QuoteAsync(RefundQuoteRequest request, CancellationToken cancellationToken = default);

        Task<AcceptedRefund> AcceptQuotedRefundAsync(
            AcceptedQuotedRefundSelection selection,
            CancellationToken cancellationToken = default);
    }

    public sealed record RefundQuoteRequest(
        long OrderId,
        int CommercialVersion,
        long ElectronicTicketId,
        string DocumentNumber,
        IReadOnlyList<long> TicketCouponIds,
        int SaleCurrencyId);

    public sealed record AcceptedQuotedRefundSelection(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedRefundId,
        int ExpectedCommercialVersion,
        long ElectronicTicketId,
        string DocumentNumber,
        IReadOnlyList<long> TicketCouponIds,
        int SaleCurrencyId);
}
