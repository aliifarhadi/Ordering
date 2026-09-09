namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public interface IRefundService
    {
        Task<RefundQuoteOutcome> QuoteAsync(
            long orderId,
            long electronicTicketId,
            CancellationToken cancellationToken = default);

        Task<RefundOutcome> RefundAsync(
            long orderId,
            long electronicTicketId,
            string quotedRefundId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }
}
