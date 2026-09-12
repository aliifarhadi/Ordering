using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.EmdExchange;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.EmdExchange
{
    public sealed class DeterministicEmdExchangePortTests : EmdExchangePortContract
    {
        protected override IEmdExchangePort Port() => new DeterministicEmdExchangeAdapter();

        [Fact]
        public async Task An_exchange_the_caller_never_saw_is_still_recoverable()
        {
            var adapter = new DeterministicEmdExchangeAdapter
            {
                ThrowAfterDispatch = true,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.ExchangeAsync(EmdExchangePortFixture.Request()));

            adapter.ThrowAfterDispatch = false;

            var recovery = await adapter.RecoverAsync(
                EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.NotNull(recovery.Successor);
            Assert.Single(adapter.DispatchedKeys);
        }

        [Fact]
        public async Task A_request_that_never_left_ordering_is_reported_as_never_dispatched()
        {
            var adapter = new DeterministicEmdExchangeAdapter { ThrowBeforeDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.ExchangeAsync(EmdExchangePortFixture.Request()));

            adapter.ThrowBeforeDispatch = false;

            var recovery = await adapter.RecoverAsync(
                EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key));

            Assert.False(recovery.WasDispatched);
            Assert.Empty(adapter.DispatchedKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task An_unresolved_exchange_names_no_successor_until_it_resolves(
            ProviderOperationOutcome unresolved)
        {
            var adapter = new DeterministicEmdExchangeAdapter
            {
                ExchangeOutcome = unresolved,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            var dispatched = await adapter.ExchangeAsync(EmdExchangePortFixture.Request());
            var recovery = await adapter.RecoverAsync(
                EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key));

            Assert.Equal(unresolved, dispatched.Outcome);
            Assert.Null(dispatched.Successor);
            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
        }

        [Fact]
        public async Task A_resolved_refusal_is_never_rewritten_by_a_later_read_back()
        {
            var adapter = new DeterministicEmdExchangeAdapter
            {
                ExchangeOutcome = ProviderOperationOutcome.Rejected,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            await adapter.ExchangeAsync(EmdExchangePortFixture.Request());

            var recovery = await adapter.RecoverAsync(
                EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Rejected, recovery.Outcome);
            Assert.Null(recovery.Successor);
            Assert.Null(recovery.ProviderReference);
        }

        [Fact]
        public async Task A_contradictory_echo_is_reported_verbatim_so_ordering_can_refuse_it()
        {
            var adapter = new DeterministicEmdExchangeAdapter
            {
                SuccessorTypeOverride = ElectronicMiscDocumentType.Standalone,
                SuccessorCurrencyOverride = 77
            };

            var result = await adapter.ExchangeAsync(EmdExchangePortFixture.Request());

            Assert.Equal(ProviderOperationOutcome.Confirmed, result.Outcome);
            Assert.Equal(ElectronicMiscDocumentType.Standalone, result.Successor!.Type);
            Assert.Equal(77, result.Successor.CurrencyId);
        }

        [Fact]
        public async Task An_unreachable_authority_is_never_read_as_a_negative_answer()
        {
            var adapter = new DeterministicEmdExchangeAdapter { ThrowOnRecover = true };

            await adapter.ExchangeAsync(EmdExchangePortFixture.Request());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.RecoverAsync(EmdExchangePortFixture.Recovery(EmdExchangePortFixture.Key)));

            Assert.Single(adapter.DispatchedKeys);
        }
    }
}
