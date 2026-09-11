using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.EmdAssociation;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.EmdAssociation
{
    public abstract class EmdAssociationPortContract
    {
        protected abstract IEmdAssociationPort Port();

        [Fact]
        public async Task A_confirmed_reassociation_names_the_coupon_it_moved_and_where_it_moved_it()
        {
            var request = EmdAssociationPortFixture.Request();
            var result = await Port().ReassociateAsync(request);

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.False(string.IsNullOrWhiteSpace(result.ProviderReference));

            if (result.EmdDocumentNumber is not null)
                Assert.Equal(request.EmdDocumentNumber, result.EmdDocumentNumber);

            if (result.EmdCouponNumber is not null)
                Assert.Equal(request.EmdCouponNumber, result.EmdCouponNumber);

            if (result.AssociatedDocumentNumber is not null)
                Assert.Equal(request.SuccessorDocumentNumber, result.AssociatedDocumentNumber);

            if (result.AssociatedCouponNumber is not null)
                Assert.Equal(request.SuccessorCouponNumber, result.AssociatedCouponNumber);
        }

        [Fact]
        public async Task Recovering_a_reassociation_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.NeverDispatchedKey));

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Null(recovery.ProviderReference);
        }

        [Fact]
        public async Task A_dispatched_reassociation_is_recoverable_under_its_own_operation_key()
        {
            var port = Port();
            var dispatched = await port.ReassociateAsync(EmdAssociationPortFixture.Request());

            var recovery = await port.RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(recovery.Outcome, recovery.AsResult().Outcome);

            if (dispatched.Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected)
            {
                Assert.Equal(dispatched.Outcome, recovery.Outcome);
                Assert.Equal(dispatched.ProviderReference, recovery.ProviderReference);
                Assert.Equal(dispatched.AssociatedDocumentNumber, recovery.AssociatedDocumentNumber);
                Assert.Equal(dispatched.AssociatedCouponNumber, recovery.AssociatedCouponNumber);
            }
        }

        [Fact]
        public async Task Reassociating_the_same_operation_key_twice_never_moves_the_coupon_twice()
        {
            var port = Port();

            var first = await port.ReassociateAsync(EmdAssociationPortFixture.Request());
            var second = await port.ReassociateAsync(EmdAssociationPortFixture.Request());

            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.AssociatedDocumentNumber, second.AssociatedDocumentNumber);
            Assert.Equal(first.AssociatedCouponNumber, second.AssociatedCouponNumber);
        }

        [Fact]
        public async Task An_operation_key_cannot_be_reused_for_a_different_association_move()
        {
            var port = Port();

            await port.ReassociateAsync(EmdAssociationPortFixture.Request());

            foreach (var conflicting in EmdAssociationPortFixture.ConflictingRequests())
                await Assert.ThrowsAnyAsync<Exception>(() => port.ReassociateAsync(conflicting));
        }

        [Fact]
        public async Task One_operation_key_can_never_consume_the_reassociation_of_another_operation()
        {
            var port = Port();
            var mine = await port.ReassociateAsync(EmdAssociationPortFixture.Request());

            var other = await port.RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.OtherKey));

            Assert.False(other.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, other.Outcome);
            Assert.NotEqual(mine.ProviderReference, other.ProviderReference);
        }

        [Fact]
        public async Task A_read_back_never_rewrites_the_evidence_of_a_dispatched_reassociation()
        {
            var port = Port();
            var dispatched = await port.ReassociateAsync(EmdAssociationPortFixture.Request());

            var first = await port.RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.Key));
            var second = await port.RecoverReassociationAsync(
                EmdAssociationPortFixture.Recovery(EmdAssociationPortFixture.Key));

            Assert.True(first.WasDispatched);
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.AssociatedDocumentNumber, second.AssociatedDocumentNumber);
            Assert.Equal(first.AssociatedCouponNumber, second.AssociatedCouponNumber);

            if (dispatched.Outcome == ProviderOperationOutcome.Confirmed)
                Assert.Equal(dispatched.ProviderReference, first.ProviderReference);
        }

        [Fact]
        public async Task A_reassociation_never_reports_a_local_identity_it_cannot_know()
        {
            var port = Port();
            var result = await port.ReassociateAsync(EmdAssociationPortFixture.Request());

            Assert.All(
                new[] { result.ProviderReference, result.EmdDocumentNumber, result.AssociatedDocumentNumber },
                reported => Assert.False(reported is not null && long.TryParse(reported, out _)));
        }
    }
}
