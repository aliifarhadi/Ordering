using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.Exchange;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeQuote
{
    public abstract class ExchangeQuotePortContract
    {
        protected abstract IExchangeQuotePort Port(ExchangeQuoteRequest request);

        [Fact]
        public async Task A_quote_answers_the_request_it_was_asked()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);

            Assert.Equal(request.OrderId, quote.OrderId);
            Assert.Equal(request.CommercialVersion, quote.ExpectedCommercialVersion);
            Assert.Equal(request.SaleCurrencyId, quote.SaleCurrencyId);
            Assert.Equal(request.PredecessorElectronicTicketId, quote.PredecessorElectronicTicketId);
            Assert.Equal(request.ChangedOrderServiceIds, quote.ChangedOrderServiceIds);
            Assert.False(string.IsNullOrWhiteSpace(quote.QuotedExchangeId));
            Assert.False(string.IsNullOrWhiteSpace(quote.SourceSystem));
            Assert.False(string.IsNullOrWhiteSpace(quote.TargetSelectionRef));
            Assert.True(quote.ExpiresAt > DateTimeOffset.UtcNow);
            Assert.True(Enum.IsDefined(quote.MonetaryOutcome));
        }

        [Fact]
        public async Task A_quote_prices_every_coupon_in_the_exchange_scope_and_no_other()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);

            Assert.Equal(
                request.ExchangeScope.Select(coupon => coupon.CouponNumber).Order(),
                quote.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.All(
                request.HistoricalUsedCoupons,
                historical => Assert.DoesNotContain(
                    quote.Coupons,
                    coupon => coupon.PredecessorCouponNumber == historical.CouponNumber
                              || coupon.PredecessorTicketCouponId == historical.PredecessorTicketCouponId));

            foreach (var coupon in quote.Coupons)
            {
                var scoped = Assert.Single(
                    request.ExchangeScope,
                    candidate => candidate.CouponNumber == coupon.PredecessorCouponNumber);

                Assert.Equal(scoped.PredecessorTicketCouponId, coupon.PredecessorTicketCouponId);
                Assert.Equal(scoped.CurrentOrderServiceId, coupon.PredecessorOrderServiceId);
                Assert.Equal(scoped.ServiceIsChanging, coupon.IsReplaced);
                Assert.NotNull(coupon.Successor);

                if (coupon.IsReplaced)
                    Assert.NotNull(coupon.Replacement);
                else
                    Assert.Null(coupon.Replacement);
            }
        }

        [Fact]
        public async Task A_quote_never_reports_ordering_as_the_pricing_source()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);

            Assert.NotEqual(PricingSource.OrderingDerived, quote.PricingSource);
            Assert.True(Enum.IsDefined(quote.PricingSource));
        }

        [Fact]
        public async Task A_quote_carries_no_value_from_a_coupon_that_is_only_history()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);
            var historical = request.PredecessorPricing
                .Where(evidence => request.HistoricalUsedCoupons.Any(coupon => coupon.CouponNumber == evidence.CouponNumber))
                .Select(evidence => evidence.CorrelationRef)
                .ToHashSet(StringComparer.Ordinal);

            Assert.NotEmpty(historical);
            Assert.DoesNotContain(
                quote.PricingLines,
                line => line.PredecessorCorrelationRef is { } reference && historical.Contains(reference));
            Assert.DoesNotContain(
                quote.Coupons.SelectMany(coupon => coupon.Successor.PriceLinks),
                link => historical.Any(reference => link.SourceLineRef.Contains(reference, StringComparison.Ordinal)));
        }

        [Fact]
        public async Task An_even_quote_transfers_the_same_value_it_withdraws()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);

            if (quote.MonetaryOutcome != ChangeMonetaryOutcome.Even)
                return;

            var withdrawn = quote.PricingLines
                .Where(line => line.Direction == OrderPricingLineDirection.Credit)
                .Sum(line => line.SaleAmount);
            var applied = quote.PricingLines
                .Where(line => line.Direction == OrderPricingLineDirection.Debit)
                .Sum(line => line.SaleAmount);

            Assert.Equal(withdrawn, applied);
            Assert.All(quote.PricingLines, line => Assert.Equal(request.SaleCurrencyId, line.SaleCurrencyId));
        }

        [Fact]
        public async Task Accepting_a_quoted_exchange_twice_under_one_operation_key_answers_the_same_plan()
        {
            var request = ExchangeQuotePortFixture.Request();
            var port = Port(request);
            var quote = await port.QuoteAsync(request);
            var selection = ExchangeQuotePortFixture.Selection(quote.QuotedExchangeId);

            var first = await port.AcceptQuotedExchangeAsync(selection);
            var second = await port.AcceptQuotedExchangeAsync(selection);

            Assert.Equal(quote.QuotedExchangeId, first.QuotedExchangeId);
            Assert.Equal(first.QuotedExchangeId, second.QuotedExchangeId);
            Assert.Equal(first.TargetSelectionRef, second.TargetSelectionRef);
            Assert.Equal(first.MonetaryOutcome, second.MonetaryOutcome);
            Assert.Equal(
                first.Coupons.Select(coupon => coupon.PredecessorTicketCouponId),
                second.Coupons.Select(coupon => coupon.PredecessorTicketCouponId));
            Assert.Equal(
                first.PricingLines.Select(line => line.SourceLineRef),
                second.PricingLines.Select(line => line.SourceLineRef));
        }

        [Fact]
        public async Task An_accepted_plan_repeats_the_scope_the_quote_reported()
        {
            var request = ExchangeQuotePortFixture.Request();
            var port = Port(request);
            var quote = await port.QuoteAsync(request);
            var accepted = await port.AcceptQuotedExchangeAsync(
                ExchangeQuotePortFixture.Selection(quote.QuotedExchangeId));

            Assert.Equal(request.OrderId, accepted.OrderId);
            Assert.Equal(request.CommercialVersion, accepted.ExpectedCommercialVersion);
            Assert.Equal(request.PredecessorElectronicTicketId, accepted.PredecessorElectronicTicketId);
            Assert.Equal(request.ChangedOrderServiceIds, accepted.ChangedOrderServiceIds);
            Assert.Equal(
                quote.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order(),
                accepted.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.NotEqual(PricingSource.OrderingDerived, accepted.PricingSource);
        }

        [Fact]
        public async Task A_quote_is_free_of_side_effects_and_answers_the_same_way_twice()
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
        public async Task A_stored_fare_construction_is_carried_without_changing_the_reissue_scope()
        {
            var plain = ExchangeQuotePortFixture.Request();
            var withConstruction = ExchangeQuotePortFixture.RequestWithStoredFareConstruction();

            Assert.NotEmpty(withConstruction.FareConstructions);
            Assert.Empty(plain.FareConstructions);

            var quote = await Port(withConstruction).QuoteAsync(withConstruction);

            Assert.Equal(
                withConstruction.ExchangeScope.Select(coupon => coupon.CouponNumber).Order(),
                quote.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.All(
                withConstruction.HistoricalUsedCoupons,
                historical => Assert.DoesNotContain(
                    quote.Coupons,
                    coupon => coupon.PredecessorCouponNumber == historical.CouponNumber));
        }

        [Fact]
        public async Task An_operation_key_that_already_owns_a_different_acceptance_intent_is_refused()
        {
            var request = ExchangeQuotePortFixture.Request();
            var port = Port(request);
            var quote = await port.QuoteAsync(request);
            var selection = ExchangeQuotePortFixture.Selection(quote.QuotedExchangeId);

            await port.AcceptQuotedExchangeAsync(selection);

            await Assert.ThrowsAnyAsync<Exception>(() => port.AcceptQuotedExchangeAsync(
                selection with { ExpectedCommercialVersion = selection.ExpectedCommercialVersion + 1 }));
        }

        [Fact]
        public async Task Accepting_a_quoted_exchange_that_was_never_quoted_is_refused()
        {
            var request = ExchangeQuotePortFixture.Request();
            var port = Port(request);

            await port.QuoteAsync(request);

            await Assert.ThrowsAnyAsync<Exception>(() => port.AcceptQuotedExchangeAsync(
                ExchangeQuotePortFixture.Selection("EXC-QUOTE-THAT-WAS-NEVER-ISSUED")));
        }
    }
}
