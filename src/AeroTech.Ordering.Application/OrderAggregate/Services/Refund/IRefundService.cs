namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public interface IRefundService
    {
        Task<RefundQuoteOutcome> QuoteAsync(
            long orderId,
            long electronicTicketId,
            IReadOnlyList<long>? ticketCouponIds = null,
            CancellationToken cancellationToken = default);

        Task<RefundOutcome> RefundAsync(RefundExecution execution, CancellationToken cancellationToken = default);
    }
}
