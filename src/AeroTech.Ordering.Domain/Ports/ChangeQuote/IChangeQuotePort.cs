using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;

namespace AeroTech.Ordering.Domain.Ports.ChangeQuote
{
    public interface IChangeQuotePort
    {
        Task<OrderAggregate.AcceptedSource.VoluntaryChange.ChangeQuote> QuoteAsync(
            ChangeQuoteRequest request,
            CancellationToken cancellationToken = default);

        Task<AcceptedVoluntaryChange> AcceptQuotedChangeAsync(
            AcceptedQuotedChangeSelection selection,
            CancellationToken cancellationToken = default);
    }
}
