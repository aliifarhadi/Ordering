using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
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
            Assert.True(quote.ChangedOrderServiceIds.ToHashSet().SetEquals(request.ChangedOrderServiceIds));
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

            Assert.True(
                quote.Coupons.Select(coupon => coupon.PredecessorCouponNumber).ToHashSet()
                    .SetEquals(request.ExchangeScope.Select(coupon => coupon.CouponNumber)));

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
        public async Task A_coupon_that_is_only_history_never_becomes_part_of_the_reissue()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);

            Assert.NotEmpty(request.HistoricalUsedCoupons);
            Assert.All(request.HistoricalUsedCoupons, historical =>
            {
                Assert.DoesNotContain(
                    quote.Coupons,
                    coupon => coupon.PredecessorCouponNumber == historical.CouponNumber
                              || coupon.PredecessorTicketCouponId == historical.PredecessorTicketCouponId
                              || coupon.PredecessorOrderServiceId == historical.CurrentOrderServiceId);
                Assert.DoesNotContain(
                    quote.Coupons.Where(coupon => coupon.Disposition is ExchangeCouponDisposition.Replaced
                                                  or ExchangeCouponDisposition.Continued),
                    coupon => coupon.PredecessorCouponNumber == historical.CouponNumber);
            });
        }

        [Fact]
        public async Task Historical_value_is_never_mechanically_attributed_to_a_successor_coupon()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);
            var reissued = request.ExchangeScope.Select(coupon => coupon.CouponNumber).ToHashSet();
            var reissuedCorrelations = request.PredecessorPricing
                .Where(evidence => reissued.Contains(evidence.CouponNumber))
                .Select(evidence => evidence.CorrelationRef)
                .ToHashSet(StringComparer.Ordinal);
            var linesBySourceRef = quote.PricingLines.ToDictionary(line => line.SourceLineRef, StringComparer.Ordinal);

            Assert.Contains(
                request.PredecessorPricing,
                evidence => !reissued.Contains(evidence.CouponNumber));

            foreach (var link in quote.Coupons.SelectMany(coupon => coupon.Successor.PriceLinks))
            {
                Assert.True(
                    linesBySourceRef.TryGetValue(link.SourceLineRef, out var attributed),
                    $"successor attribution {link.SourceLineRef} does not resolve to a quoted pricing line");
                Assert.Equal(request.SaleCurrencyId, link.CurrencyId);

                if (attributed!.LineRole != PricingLineRole.Transfer
                    || attributed.PredecessorCorrelationRef is not { } correlation)
                    continue;

                Assert.Contains(correlation, reissuedCorrelations);
            }
        }

        [Fact]
        public async Task An_even_quote_leaves_the_customer_balance_untouched()
        {
            var request = ExchangeQuotePortFixture.Request();
            var quote = await Port(request).QuoteAsync(request);

            if (quote.MonetaryOutcome != ChangeMonetaryOutcome.Even)
                return;

            Assert.Equal(0m, ExchangePricingPolicy.NetCustomerBalance(quote.PricingLines));
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
            Assert.True(
                first.Coupons.Select(coupon => coupon.PredecessorCouponNumber).ToHashSet()
                    .SetEquals(second.Coupons.Select(coupon => coupon.PredecessorCouponNumber)));
            Assert.True(
                first.PricingLines.Select(line => line.SourceLineRef).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(second.PricingLines.Select(line => line.SourceLineRef)));
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
            Assert.True(accepted.ChangedOrderServiceIds.ToHashSet().SetEquals(request.ChangedOrderServiceIds));
            Assert.True(
                accepted.Coupons.Select(coupon => coupon.PredecessorCouponNumber).ToHashSet()
                    .SetEquals(quote.Coupons.Select(coupon => coupon.PredecessorCouponNumber)));
            Assert.NotEqual(PricingSource.OrderingDerived, accepted.PricingSource);
        }

        [Fact]
        public async Task A_repeated_independent_quote_still_answers_the_request_it_was_asked()
        {
            var request = ExchangeQuotePortFixture.Request();
            var port = Port(request);

            var first = await port.QuoteAsync(request);
            var second = await port.QuoteAsync(request);

            Assert.False(string.IsNullOrWhiteSpace(second.QuotedExchangeId));
            Assert.Equal(first.OrderId, second.OrderId);
            Assert.Equal(first.ExpectedCommercialVersion, second.ExpectedCommercialVersion);
            Assert.Equal(first.PredecessorElectronicTicketId, second.PredecessorElectronicTicketId);
            Assert.True(
                second.ChangedOrderServiceIds.ToHashSet().SetEquals(first.ChangedOrderServiceIds));
            Assert.True(
                second.Coupons.Select(coupon => coupon.PredecessorCouponNumber).ToHashSet()
                    .SetEquals(first.Coupons.Select(coupon => coupon.PredecessorCouponNumber)));
            Assert.NotEqual(PricingSource.OrderingDerived, second.PricingSource);
        }

        [Fact]
        public async Task A_stored_fare_construction_is_carried_without_changing_the_reissue_scope()
        {
            var plain = ExchangeQuotePortFixture.Request();
            var withConstruction = ExchangeQuotePortFixture.RequestWithStoredFareConstruction();

            Assert.NotEmpty(withConstruction.FareConstructions);
            Assert.Empty(plain.FareConstructions);

            var quote = await Port(withConstruction).QuoteAsync(withConstruction);

            Assert.True(
                quote.Coupons.Select(coupon => coupon.PredecessorCouponNumber).ToHashSet()
                    .SetEquals(withConstruction.ExchangeScope.Select(coupon => coupon.CouponNumber)));
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
