using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeQuote
{
    public sealed class DeterministicExchangeQuotePortTests : ExchangeQuotePortContract
    {
        protected override IExchangeQuotePort Port(ExchangeQuoteRequest request)
            => new DeterministicExchangeQuoteAdapter
            {
                Composer = quoted => ExchangeSourceFactory.Compose(quoted, ExchangeQuotePortFixture.Replacements())
            };

        [Fact]
        public async Task The_simulator_repeats_one_deterministic_answer_for_the_same_request()
        {
            var request = ExchangeQuotePortFixture.Request();
            var port = Port(request);

            var first = await port.QuoteAsync(request);
            var second = await port.QuoteAsync(request);

            Assert.Equal(first.QuotedExchangeId, second.QuotedExchangeId);
            Assert.Equal(first.MonetaryOutcome, second.MonetaryOutcome);
            Assert.Equal(
                first.Coupons.Select(coupon => coupon.PredecessorCouponNumber),
                second.Coupons.Select(coupon => coupon.PredecessorCouponNumber));
            Assert.Equal(
                first.PricingLines.Select(line => line.SourceLineRef),
                second.PricingLines.Select(line => line.SourceLineRef));
        }

        [Fact]
        public async Task The_simulator_builds_transfer_lines_only_from_open_scope_evidence()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);
            var reissued = request.ExchangeScope.Select(coupon => coupon.CouponNumber).ToHashSet();
            var historicalCorrelations = request.PredecessorPricing
                .Where(evidence => !reissued.Contains(evidence.CouponNumber))
                .Select(evidence => evidence.CorrelationRef)
                .ToHashSet(StringComparer.Ordinal);

            Assert.NotEmpty(historicalCorrelations);
            Assert.DoesNotContain(
                quote.PricingLines,
                line => line.PredecessorCorrelationRef is { } correlation && historicalCorrelations.Contains(correlation));
        }
    }
}
