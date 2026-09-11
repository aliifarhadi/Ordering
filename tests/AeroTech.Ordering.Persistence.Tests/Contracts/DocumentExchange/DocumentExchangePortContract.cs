using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.DocumentExchange
{
    public abstract class DocumentExchangePortContract
    {
        protected abstract IDocumentExchangePort Port();

        [Fact]
        public async Task An_eligibility_answer_is_observational_and_repeatable()
        {
            var port = Port();
            var request = DocumentExchangePortFixture.EligibilityRequest();

            var first = await port.CheckEligibilityAsync(request);
            var second = await port.CheckEligibilityAsync(request);

            Assert.True(Enum.IsDefined(first.Outcome));
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.False((await port.RecoverAsync(DocumentExchangePortFixture.Recovery())).WasDispatched);
        }

        [Fact]
        public async Task A_confirmed_exchange_names_a_successor_document_and_a_provider_reference()
        {
            var result = await Port().ExchangeAsync(DocumentExchangePortFixture.Request());

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.False(string.IsNullOrWhiteSpace(result.ProviderReference));
            Assert.NotNull(result.Successor);
            Assert.False(string.IsNullOrWhiteSpace(result.Successor!.DocumentNumber));
            Assert.True(result.Successor.IssuerCarrierId > 0);
            Assert.True(Enum.IsDefined(result.Successor.Authority));
        }

        [Fact]
        public async Task Every_successor_coupon_maps_to_a_predecessor_coupon_the_request_named()
        {
            var request = DocumentExchangePortFixture.Request();
            var result = await Port().ExchangeAsync(request);

            if (result.Successor is null)
                return;

            var asked = request.Coupons.Select(coupon => coupon.PredecessorCouponNumber).ToList();
            var mapped = result.Successor.Coupons;

            Assert.Equal(asked.Count, mapped.Count);
            Assert.All(mapped, coupon => Assert.Contains(coupon.PredecessorCouponNumber, asked));
            Assert.Equal(
                mapped.Select(coupon => coupon.PredecessorCouponNumber).Distinct().Count(),
                mapped.Count);
            Assert.Equal(
                mapped.Select(coupon => coupon.CouponNumber).Distinct().Count(),
                mapped.Count);
            Assert.All(mapped, coupon => Assert.True(coupon.CouponNumber > 0));
        }

        [Fact]
        public async Task No_coupon_outside_the_reissue_scope_crosses_the_document_boundary()
        {
            var request = DocumentExchangePortFixture.Request();
            var result = await Port().ExchangeAsync(request);

            Assert.Equal(
                [DocumentExchangePortFixture.ContinuedCouponNumber, DocumentExchangePortFixture.ReplacedCouponNumber],
                request.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.DoesNotContain(
                DocumentExchangePortFixture.UsedCouponNumber,
                request.Coupons.Select(coupon => coupon.PredecessorCouponNumber));
            Assert.All(request.Coupons, coupon => Assert.True(coupon.Segment.IsComplete));
            Assert.DoesNotContain(
                DocumentExchangePortFixture.UsedCouponNumber,
                (result.Successor?.Coupons ?? []).Select(coupon => coupon.PredecessorCouponNumber));
        }

        [Fact]
        public async Task The_boundary_correlates_on_the_document_number_and_the_predecessor_coupon_number()
        {
            var request = DocumentExchangePortFixture.Request();
            var eligibility = DocumentExchangePortFixture.EligibilityRequest();
            var recovery = DocumentExchangePortFixture.Recovery();

            Assert.Equal(request.PredecessorDocumentNumber, eligibility.PredecessorDocumentNumber);
            Assert.Equal(request.PredecessorDocumentNumber, recovery.PredecessorDocumentNumber);
            Assert.Equal(request.OperationKey, recovery.OperationKey);
            Assert.Equal(
                request.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order(),
                eligibility.PredecessorCouponNumbers.Order());

            var result = await Port().ExchangeAsync(request);

            if (result.Successor is null)
                return;

            Assert.All(
                result.Successor.Coupons,
                coupon => Assert.Single(request.Coupons, asked => asked.PredecessorCouponNumber == coupon.PredecessorCouponNumber));
        }

        [Fact]
        public async Task Recovering_an_operation_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverAsync(DocumentExchangePortFixture.UnknownRecovery());

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Null(recovery.Successor);
        }

        [Fact]
        public async Task Recovering_a_dispatched_exchange_answers_under_the_same_operation_key_without_exchanging_again()
        {
            var port = Port();
            var request = DocumentExchangePortFixture.Request();

            var dispatched = await port.ExchangeAsync(request);

            var first = await port.RecoverAsync(DocumentExchangePortFixture.Recovery());
            var second = await port.RecoverAsync(DocumentExchangePortFixture.Recovery());

            Assert.True(first.WasDispatched);
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.Successor?.DocumentNumber, second.Successor?.DocumentNumber);
            Assert.Equal(first.Outcome, first.AsResult().Outcome);
            Assert.Equal(dispatched.Successor?.DocumentNumber, first.Successor?.DocumentNumber);

            if (first.Successor is null)
                return;

            Assert.All(
                first.Successor.Coupons,
                coupon => Assert.Contains(coupon.PredecessorCouponNumber, request.Coupons.Select(asked => asked.PredecessorCouponNumber)));
        }
    }
}
