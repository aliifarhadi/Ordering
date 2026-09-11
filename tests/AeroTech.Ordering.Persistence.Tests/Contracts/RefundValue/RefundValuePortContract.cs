using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.RefundValue;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.RefundValue
{
    public abstract class RefundValuePortContract
    {
        protected abstract IRefundValuePort Port();

        [Fact]
        public async Task A_return_of_value_reports_a_defined_outcome_for_the_obligation_it_was_asked()
        {
            var request = RefundValuePortFixture.Request();
            var result = await Port().RequestAsync(request);

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.False(string.IsNullOrWhiteSpace(result.ValueMovementReference));
            Assert.Equal(request.ApprovedAmount, result.Amount);
            Assert.Equal(request.CurrencyId, result.CurrencyId);
            Assert.Equal(request.ApprovedDisposition, result.Disposition);
        }

        [Fact]
        public async Task Recovering_a_return_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverAsync(
                RefundValuePortFixture.Recovery(RefundValuePortFixture.NeverDispatchedKey));

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Null(recovery.ValueMovementReference);
        }

        [Fact]
        public async Task A_dispatched_return_is_recoverable_under_its_own_operation_key()
        {
            var port = Port();
            var dispatched = await port.RequestAsync(RefundValuePortFixture.Request());

            var recovery = await port.RecoverAsync(RefundValuePortFixture.Recovery(RefundValuePortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.True(Enum.IsDefined(recovery.Outcome));
            Assert.Equal(recovery.Outcome, recovery.AsResult().Outcome);

            if (dispatched.Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected)
            {
                Assert.Equal(dispatched.Outcome, recovery.Outcome);
                Assert.Equal(dispatched.ValueMovementReference, recovery.ValueMovementReference);
            }
        }

        [Fact]
        public async Task Requesting_the_same_operation_key_twice_never_pays_twice()
        {
            var port = Port();

            var first = await port.RequestAsync(RefundValuePortFixture.Request());
            var second = await port.RequestAsync(RefundValuePortFixture.Request());

            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ValueMovementReference, second.ValueMovementReference);
            Assert.Equal(first.Amount, second.Amount);
            Assert.Equal(first.CurrencyId, second.CurrencyId);
        }

        [Fact]
        public async Task An_operation_key_cannot_be_reused_for_a_different_obligation()
        {
            var port = Port();

            await port.RequestAsync(RefundValuePortFixture.Request());

            foreach (var conflicting in RefundValuePortFixture.ConflictingRequests())
                await Assert.ThrowsAnyAsync<Exception>(() => port.RequestAsync(conflicting));
        }

        [Fact]
        public async Task One_operation_key_can_never_consume_another_operations_result()
        {
            var port = Port();
            var mine = await port.RequestAsync(RefundValuePortFixture.Request());

            var other = await port.RecoverAsync(RefundValuePortFixture.Recovery(RefundValuePortFixture.OtherKey));

            Assert.False(other.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, other.Outcome);
            Assert.NotEqual(mine.ValueMovementReference, other.ValueMovementReference);
        }

        [Fact]
        public async Task A_read_back_never_rewrites_the_evidence_of_a_dispatched_return()
        {
            var port = Port();
            var dispatched = await port.RequestAsync(RefundValuePortFixture.Request());

            var first = await port.RecoverAsync(RefundValuePortFixture.Recovery(RefundValuePortFixture.Key));
            var second = await port.RecoverAsync(RefundValuePortFixture.Recovery(RefundValuePortFixture.Key));

            Assert.True(first.WasDispatched);
            Assert.Equal(first.Amount, second.Amount);
            Assert.Equal(first.CurrencyId, second.CurrencyId);
            Assert.Equal(first.ValueMovementReference, second.ValueMovementReference);

            if (dispatched.Amount is not null)
                Assert.Equal(dispatched.Amount, first.Amount);
        }
    }
}
