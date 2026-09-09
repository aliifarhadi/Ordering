namespace AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange
{
    public interface IVoluntaryChangeService
    {
        Task<ChangeQuoteOutcome> QuoteAsync(
            long orderId,
            long orderServiceId,
            CancellationToken cancellationToken = default);

        Task<VoluntaryChangeOutcome> ChangeAsync(
            VoluntaryChangeExecution execution,
            CancellationToken cancellationToken = default);
    }
}
