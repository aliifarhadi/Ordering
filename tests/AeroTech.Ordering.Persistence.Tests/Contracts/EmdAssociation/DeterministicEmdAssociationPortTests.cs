using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.EmdAssociation;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.EmdAssociation
{
    public sealed class DeterministicEmdAssociationPortTests : EmdAssociationPortContract
    {
        protected override IEmdAssociationPort Port() => new DeterministicEmdAssociationAdapter();

        [Fact]
        public async Task A_move_the_caller_never_saw_is_still_recoverable_and_never_repeated()
        {
            var adapter = new DeterministicEmdAssociationAdapter { ThrowAfterDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.ReassociateAsync(EmdAssociationPortFixture.Request()));

            adapter.ThrowAfterDispatch = false;

            var recovery = await adapter.RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal(EmdAssociationPortFixture.SuccessorDocumentNumber, recovery.AssociatedDocumentNumber);
            Assert.Single(adapter.ObservedRequests);
        }

        [Fact]
        public async Task A_request_that_never_left_ordering_is_reported_as_never_dispatched()
        {
            var adapter = new DeterministicEmdAssociationAdapter { ThrowBeforeDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.ReassociateAsync(EmdAssociationPortFixture.Request()));

            adapter.ThrowBeforeDispatch = false;

            var recovery = await adapter.RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.Key));

            Assert.False(recovery.WasDispatched);
            Assert.Empty(adapter.DispatchedKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task An_unresolved_move_keeps_its_evidence_and_resolves_on_read_back(
            ProviderOperationOutcome unresolved)
        {
            var adapter = new DeterministicEmdAssociationAdapter
            {
                ReassociateOutcome = unresolved,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            var dispatched = await adapter.ReassociateAsync(EmdAssociationPortFixture.Request());
            var recovery = await adapter.RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.Key));

            Assert.Equal(unresolved, dispatched.Outcome);
            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal(dispatched.ProviderReference, recovery.ProviderReference);
            Assert.Single(adapter.ObservedRequests);
        }

        [Fact]
        public async Task A_refused_move_stays_refused_and_names_no_provider_reference()
        {
            var adapter = new DeterministicEmdAssociationAdapter
            {
                ReassociateOutcome = ProviderOperationOutcome.Rejected,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            await adapter.ReassociateAsync(EmdAssociationPortFixture.Request());

            var recovery = await adapter.RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Rejected, recovery.Outcome);
            Assert.Null(recovery.ProviderReference);
        }
    }
}
