using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.RefundValue;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.RefundValue
{
    public sealed class DeterministicRefundValuePortTests : RefundValuePortContract
    {
        protected override IRefundValuePort Port() => new DeterministicRefundValueAdapter();

        [Fact]
        public async Task A_payout_the_caller_never_saw_is_still_recoverable()
        {
            var adapter = new DeterministicRefundValueAdapter { ThrowAfterDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.RequestAsync(RefundValuePortFixture.Request()));

            adapter.ThrowAfterDispatch = false;

            var recovery = await adapter.RecoverAsync(RefundValuePortFixture.Recovery(RefundValuePortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal(RefundValuePortFixture.Amount, recovery.Amount);
            Assert.Single(adapter.ObservedRequests);
        }

        [Fact]
        public async Task A_request_that_never_left_ordering_is_reported_as_never_dispatched()
        {
            var adapter = new DeterministicRefundValueAdapter { ThrowOnRequest = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.RequestAsync(RefundValuePortFixture.Request()));

            adapter.ThrowOnRequest = false;

            var recovery = await adapter.RecoverAsync(RefundValuePortFixture.Recovery(RefundValuePortFixture.Key));

            Assert.False(recovery.WasDispatched);
            Assert.Empty(adapter.DispatchedKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task An_unresolved_payout_keeps_its_evidence_and_resolves_on_read_back(ProviderOperationOutcome unresolved)
        {
            var adapter = new DeterministicRefundValueAdapter
            {
                RequestOutcome = unresolved,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            var dispatched = await adapter.RequestAsync(RefundValuePortFixture.Request());
            var recovery = await adapter.RecoverAsync(RefundValuePortFixture.Recovery(RefundValuePortFixture.Key));

            Assert.Equal(unresolved, dispatched.Outcome);
            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal(dispatched.ValueMovementReference, recovery.ValueMovementReference);
            Assert.Single(adapter.ObservedRequests);
        }
    }
}
