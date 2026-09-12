using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentRefund;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.DocumentRefund
{
    public abstract class DocumentRefundPortContract
    {
        protected abstract IDocumentRefundPort Port();

        [Fact]
        public async Task An_eligibility_answer_is_always_a_defined_outcome()
        {
            var eligibility = await Port().CheckEligibilityAsync(DocumentRefundPortFixture.Eligibility());

            Assert.True(Enum.IsDefined(eligibility.Outcome));
        }

        [Fact]
        public async Task A_refund_names_the_document_and_coupons_it_acted_on()
        {
            var request = DocumentRefundPortFixture.Request();
            var result = await Port().RefundAsync(request);

            Assert.True(Enum.IsDefined(result.Outcome));

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
                return;

            Assert.False(string.IsNullOrWhiteSpace(result.ProviderReference));
            Assert.Equal(request.DocumentNumber, result.DocumentNumber);
            Assert.Equal(request.CouponNumbers, result.CouponNumbers);
        }

        [Fact]
        public async Task Recovering_a_refund_that_was_never_dispatched_says_so_instead_of_confirming_it()
        {
            var recovery = await Port().RecoverAsync(
                DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.NeverDispatchedKey));

            Assert.False(recovery.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Null(recovery.ProviderReference);
            Assert.Null(recovery.CouponNumbers);
        }

        [Fact]
        public async Task A_dispatched_refund_is_recoverable_under_its_own_operation_key()
        {
            var port = Port();
            var dispatched = await port.RefundAsync(DocumentRefundPortFixture.Request());

            var recovery = await port.RecoverAsync(
                DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.True(Enum.IsDefined(recovery.Outcome));
            Assert.Equal(recovery.Outcome, recovery.AsResult().Outcome);
            Assert.Equal(dispatched.CouponNumbers, recovery.CouponNumbers);

            if (dispatched.Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected)
            {
                Assert.Equal(dispatched.Outcome, recovery.Outcome);
                Assert.Equal(dispatched.ProviderReference, recovery.ProviderReference);
            }
        }

        [Fact]
        public async Task Refunding_the_same_operation_key_twice_never_refunds_the_document_twice()
        {
            var port = Port();

            var first = await port.RefundAsync(DocumentRefundPortFixture.Request());
            var second = await port.RefundAsync(DocumentRefundPortFixture.Request());

            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.DocumentNumber, second.DocumentNumber);
            Assert.Equal(first.CouponNumbers, second.CouponNumbers);
        }

        [Fact]
        public async Task An_operation_key_cannot_be_reused_for_a_different_refund()
        {
            var port = Port();

            await port.RefundAsync(DocumentRefundPortFixture.Request());

            foreach (var conflicting in DocumentRefundPortFixture.ConflictingRequests())
                await Assert.ThrowsAnyAsync<Exception>(() => port.RefundAsync(conflicting));
        }

        [Fact]
        public async Task One_operation_key_can_never_consume_another_operations_refund()
        {
            var port = Port();
            var mine = await port.RefundAsync(DocumentRefundPortFixture.Request());

            var other = await port.RecoverAsync(
                DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.OtherKey));

            Assert.False(other.WasDispatched);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, other.Outcome);
            Assert.NotEqual(mine.ProviderReference, other.ProviderReference);
        }

        [Fact]
        public async Task A_read_back_never_rewrites_the_evidence_of_a_dispatched_refund()
        {
            var port = Port();
            var dispatched = await port.RefundAsync(DocumentRefundPortFixture.Request());

            var first = await port.RecoverAsync(DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key));
            var second = await port.RecoverAsync(DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key));

            Assert.True(first.WasDispatched);
            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.ProviderReference, second.ProviderReference);
            Assert.Equal(first.DocumentNumber, second.DocumentNumber);
            Assert.Equal(first.CouponNumbers, second.CouponNumbers);
            Assert.Equal(dispatched.CouponNumbers, first.CouponNumbers);
        }

        [Fact]
        public async Task No_local_ordering_identity_is_ever_reported_back()
        {
            var port = Port();
            var result = await port.RefundAsync(DocumentRefundPortFixture.Request());

            Assert.DoesNotContain(
                DocumentRefundPortFixture.OrderId.ToString(),
                result.ProviderReference ?? string.Empty,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                DocumentRefundPortFixture.OperationId.ToString(),
                result.ProviderReference ?? string.Empty,
                StringComparison.Ordinal);
        }
    }
}
