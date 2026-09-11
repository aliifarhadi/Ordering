using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeFunding
{
    public abstract class ExchangeFundingPortContract
    {
        protected abstract IExchangeFundingPort Port();

        // ---------------------------------------------------------------- guarantee

        [Fact]
        public async Task A_guarantee_reports_a_defined_outcome_for_the_amount_it_was_asked()
        {
            var request = ExchangeFundingPortFixture.Guarantee();
            var result = await Port().GuaranteeAsync(request);

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome == ProviderOperationOutcome.Rejected)
            {
                Assert.Null(result.ProviderReference);

                return;
            }

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.False(string.IsNullOrWhiteSpace(result.ProviderReference));
            Assert.Equal(request.Amount, result.Amount);
            Assert.Equal(request.CurrencyId, result.CurrencyId);
        }

        [Fact]
        public async Task Recovering_a_guarantee_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverGuaranteeAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.NeverDispatchedKey));

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Null(recovery.ProviderReference);
        }

        [Fact]
        public async Task A_dispatched_guarantee_is_recoverable_under_its_own_operation_key()
        {
            var port = Port();
            var dispatched = await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());

            var recovery = await port.RecoverGuaranteeAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.GuaranteeKey));

            Assert.True(recovery.WasDispatched);
            Assert.True(Enum.IsDefined(recovery.Outcome));
            Assert.Equal(recovery.Outcome, recovery.AsResult().Outcome);

            if (dispatched.Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected)
            {
                Assert.Equal(dispatched.Outcome, recovery.Outcome);
                Assert.Equal(dispatched.ProviderReference, recovery.ProviderReference);
            }
        }

        [Fact]
        public async Task Guaranteeing_the_same_operation_key_twice_never_moves_money_twice()
        {
            var port = Port();

            var first = await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());
            var second = await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());

            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.Amount, second.Amount);
            Assert.Equal(first.CurrencyId, second.CurrencyId);
        }

        [Fact]
        public async Task One_operation_key_can_never_consume_another_operations_result()
        {
            var port = Port();
            var mine = await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());

            var other = await port.RecoverGuaranteeAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.OtherGuaranteeKey));

            Assert.False(other.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, other.Outcome);
            Assert.NotEqual(mine.ProviderReference, other.ProviderReference);

            var theirs = await port.GuaranteeAsync(
                ExchangeFundingPortFixture.Guarantee(ExchangeFundingPortFixture.OtherGuaranteeKey));

            Assert.NotEqual(mine.ProviderReference, theirs.ProviderReference);
        }

        // ---------------------------------------------------------------- capture

        [Fact]
        public async Task A_capture_reports_a_defined_outcome_for_the_amount_it_was_asked()
        {
            var port = Port();
            var guarantee = await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());

            var result = await port.CaptureAsync(ExchangeFundingPortFixture.Capture(guarantee.ProviderReference));

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.False(string.IsNullOrWhiteSpace(result.ProviderReference));
            Assert.Equal(ExchangeFundingPortFixture.Amount, result.Amount);
            Assert.Equal(ExchangeFundingPortFixture.CurrencyId, result.CurrencyId);
        }

        [Fact]
        public async Task Recovering_a_capture_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverCaptureAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.CaptureKey));

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
        }

        [Fact]
        public async Task A_dispatched_capture_is_recoverable_and_never_captures_twice()
        {
            var port = Port();
            var guarantee = await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());
            var dispatched = await port.CaptureAsync(ExchangeFundingPortFixture.Capture(guarantee.ProviderReference));

            var recovery = await port.RecoverCaptureAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.CaptureKey));
            var again = await port.CaptureAsync(ExchangeFundingPortFixture.Capture(guarantee.ProviderReference));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(dispatched.ProviderReference, again.ProviderReference);

            if (dispatched.Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected)
                Assert.Equal(dispatched.Outcome, recovery.Outcome);
        }

        // ---------------------------------------------------------------- release

        [Fact]
        public async Task A_release_reports_a_defined_outcome()
        {
            var port = Port();
            var guarantee = await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());

            var result = await port.ReleaseAsync(ExchangeFundingPortFixture.Release(guarantee.ProviderReference));

            Assert.True(Enum.IsDefined(result.Outcome));
        }

        [Fact]
        public async Task Recovering_a_release_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverReleaseAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.ReleaseKey));

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
        }

        [Fact]
        public async Task A_dispatched_release_is_recoverable_and_never_releases_twice()
        {
            var port = Port();
            var guarantee = await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());
            var dispatched = await port.ReleaseAsync(ExchangeFundingPortFixture.Release(guarantee.ProviderReference));

            var recovery = await port.RecoverReleaseAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.ReleaseKey));
            var again = await port.ReleaseAsync(ExchangeFundingPortFixture.Release(guarantee.ProviderReference));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(dispatched.Outcome, again.Outcome);
            Assert.Equal(dispatched.ProviderReference, again.ProviderReference);
        }

        // ---------------------------------------------------------------- stage isolation

        [Fact]
        public async Task Each_funding_stage_owns_its_own_operation_key()
        {
            var port = Port();

            await port.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());

            var capture = await port.RecoverCaptureAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.CaptureKey));
            var release = await port.RecoverReleaseAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.ReleaseKey));

            Assert.False(capture.WasDispatched);
            Assert.False(release.WasDispatched);
        }
    }
}
