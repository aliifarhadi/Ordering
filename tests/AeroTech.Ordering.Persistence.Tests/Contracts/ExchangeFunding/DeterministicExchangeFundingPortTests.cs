using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeFunding
{
    public sealed class DeterministicExchangeFundingPortTests : ExchangeFundingPortContract
    {
        protected override IExchangeFundingPort Port() => new DeterministicExchangeFundingAdapter();

        [Fact]
        public async Task A_confirmation_the_caller_never_saw_is_still_recoverable()
        {
            var adapter = new DeterministicExchangeFundingAdapter { ThrowAfterGuaranteeDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee()));

            adapter.ThrowAfterGuaranteeDispatch = false;

            var recovery = await adapter.RecoverGuaranteeAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.GuaranteeKey));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal(ExchangeFundingPortFixture.Amount, recovery.Amount);
            Assert.Single(adapter.ObservedGuarantees);
        }

        [Fact]
        public async Task A_request_that_never_left_ordering_is_reported_as_never_dispatched()
        {
            var adapter = new DeterministicExchangeFundingAdapter { ThrowBeforeGuaranteeDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee()));

            adapter.ThrowBeforeGuaranteeDispatch = false;

            var recovery = await adapter.RecoverGuaranteeAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.GuaranteeKey));

            Assert.False(recovery.WasDispatched);
            Assert.Empty(adapter.DispatchedKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task An_unresolved_guarantee_keeps_its_operation_and_resolves_on_read_back(ProviderOperationOutcome unresolved)
        {
            var adapter = new DeterministicExchangeFundingAdapter
            {
                GuaranteeOutcome = unresolved,
                GuaranteeRecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            var dispatched = await adapter.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());
            var recovery = await adapter.RecoverGuaranteeAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.GuaranteeKey));

            Assert.Equal(unresolved, dispatched.Outcome);
            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Single(adapter.ObservedGuarantees);
        }

        [Fact]
        public async Task A_rejected_guarantee_stays_rejected_on_read_back()
        {
            var adapter = new DeterministicExchangeFundingAdapter
            {
                GuaranteeOutcome = ProviderOperationOutcome.Rejected,
                GuaranteeRecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            await adapter.GuaranteeAsync(ExchangeFundingPortFixture.Guarantee());

            var recovery = await adapter.RecoverGuaranteeAsync(
                ExchangeFundingPortFixture.Recovery(ExchangeFundingPortFixture.GuaranteeKey));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Rejected, recovery.Outcome);
            Assert.Null(recovery.ProviderReference);
        }
    }
}
