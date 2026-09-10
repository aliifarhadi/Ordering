namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public interface IExchangeService
    {
        Task<ExchangeQuoteOutcome> QuoteAsync(
            long orderId,
            long predecessorOrderServiceId,
            CancellationToken cancellationToken = default);

        Task<ExchangeOutcome> ExchangeAsync(
            ExchangeExecution execution,
            CancellationToken cancellationToken = default);
    }
}
