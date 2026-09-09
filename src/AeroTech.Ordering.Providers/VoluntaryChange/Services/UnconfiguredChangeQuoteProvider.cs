using AeroTech.Ordering.Domain.Ports.ChangeQuote;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.VoluntaryChange.Services
{
    public sealed class UnconfiguredChangeQuoteProvider : IChangeQuotePort
    {
        public Task<Domain.OrderAggregate.AcceptedSource.VoluntaryChange.ChangeQuote> QuoteAsync(
            ChangeQuoteRequest request,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ChangeQuoteSourceNotConfigured();

        public Task<Domain.OrderAggregate.AcceptedSource.VoluntaryChange.AcceptedVoluntaryChange> AcceptQuotedChangeAsync(
            AcceptedQuotedChangeSelection selection,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.ChangeQuoteSourceNotConfigured();
    }
}
