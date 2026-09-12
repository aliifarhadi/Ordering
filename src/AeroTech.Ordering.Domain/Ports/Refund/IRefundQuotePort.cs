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
}
