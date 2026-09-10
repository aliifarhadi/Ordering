namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public interface IExchangeService
    {
        Task<ExchangeQuoteOutcome> QuoteAsync(
            long orderId,
            IReadOnlyList<long> changedOrderServiceIds,
            CancellationToken cancellationToken = default);

        Task<ExchangeOutcome> ExchangeAsync(
            ExchangeExecution execution,
            CancellationToken cancellationToken = default);
    }
}
