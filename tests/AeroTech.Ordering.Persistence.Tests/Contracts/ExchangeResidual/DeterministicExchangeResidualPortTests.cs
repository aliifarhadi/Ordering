using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeResidual;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeResidual
{
    public sealed class DeterministicExchangeResidualPortTests : ExchangeResidualPortContract
    {
        protected override IExchangeResidualValuePort Port() => new DeterministicExchangeResidualAdapter();

        [Fact]
        public async Task An_instrument_the_caller_never_saw_is_still_recoverable_and_never_duplicated()
        {
            var adapter = new DeterministicExchangeResidualAdapter { ThrowAfterDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.FulfillAsync(ExchangeResidualPortFixture.Request()));

            adapter.ThrowAfterDispatch = false;

            var recovery = await adapter.RecoverAsync(
                ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal($"INSTR-{ExchangeResidualPortFixture.SuccessorDocumentNumber}", recovery.InstrumentReference);
            Assert.Equal(ExchangeResidualPortFixture.Amount, recovery.Amount);
            Assert.Single(adapter.ObservedRequests);
        }

        [Fact]
        public async Task A_request_that_never_left_ordering_is_reported_as_never_dispatched()
        {
            var adapter = new DeterministicExchangeResidualAdapter { ThrowBeforeDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.FulfillAsync(ExchangeResidualPortFixture.Request()));

            adapter.ThrowBeforeDispatch = false;

            var recovery = await adapter.RecoverAsync(
                ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.Key));

            Assert.False(recovery.WasDispatched);
            Assert.Empty(adapter.DispatchedKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task An_unresolved_residual_keeps_its_instrument_and_resolves_on_read_back(ProviderOperationOutcome unresolved)
        {
            var adapter = new DeterministicExchangeResidualAdapter
            {
                FulfillOutcome = unresolved,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            var dispatched = await adapter.FulfillAsync(ExchangeResidualPortFixture.Request());
            var recovery = await adapter.RecoverAsync(
                ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.Key));

            Assert.Equal(unresolved, dispatched.Outcome);
            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal(dispatched.InstrumentReference, recovery.InstrumentReference);
            Assert.Single(adapter.ObservedRequests);
        }

        [Fact]
        public async Task A_refused_residual_stays_refused_and_names_no_instrument()
        {
            var adapter = new DeterministicExchangeResidualAdapter
            {
                FulfillOutcome = ProviderOperationOutcome.Rejected,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            await adapter.FulfillAsync(ExchangeResidualPortFixture.Request());

            var recovery = await adapter.RecoverAsync(
                ExchangeResidualPortFixture.Recovery(ExchangeResidualPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Rejected, recovery.Outcome);
            Assert.Null(recovery.InstrumentReference);
        }
    }
}
