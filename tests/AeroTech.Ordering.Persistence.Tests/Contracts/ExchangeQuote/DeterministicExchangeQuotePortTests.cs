using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Providers.Deterministic;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeQuote
{
    public sealed class DeterministicExchangeQuotePortTests : ExchangeQuotePortContract
    {
        protected override IExchangeQuotePort Port(ExchangeQuoteRequest request)
            => new DeterministicExchangeQuoteAdapter
            {
                Composer = quoted => ExchangeSourceFactory.Compose(quoted, ExchangeQuotePortFixture.Replacements())
            };
    }
}
