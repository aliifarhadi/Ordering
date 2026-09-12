using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentRefund;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.DocumentRefund
{
    public sealed class DeterministicDocumentRefundPortTests : DocumentRefundPortContract
    {
        protected override IDocumentRefundPort Port() => new DeterministicDocumentRefundAdapter();

        [Fact]
        public async Task A_refund_the_caller_never_saw_is_still_recoverable()
        {
            var adapter = new DeterministicDocumentRefundAdapter
            {
                ThrowAfterDispatch = true,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.RefundAsync(DocumentRefundPortFixture.Request()));

            adapter.ThrowAfterDispatch = false;

            var recovery = await adapter.RecoverAsync(
                DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal(DocumentRefundPortFixture.CouponNumbers, recovery.CouponNumbers);
            Assert.Single(adapter.DispatchedKeys);
        }

        [Fact]
        public async Task A_request_that_never_left_ordering_is_reported_as_never_dispatched()
        {
            var adapter = new DeterministicDocumentRefundAdapter { ThrowBeforeDispatch = true };

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.RefundAsync(DocumentRefundPortFixture.Request()));

            adapter.ThrowBeforeDispatch = false;

            var recovery = await adapter.RecoverAsync(
                DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key));

            Assert.False(recovery.WasDispatched);
            Assert.Empty(adapter.DispatchedKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task An_unresolved_refund_keeps_its_evidence_and_resolves_on_read_back(
            ProviderOperationOutcome unresolved)
        {
            var adapter = new DeterministicDocumentRefundAdapter
            {
                RefundOutcome = unresolved,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            var dispatched = await adapter.RefundAsync(DocumentRefundPortFixture.Request());
            var recovery = await adapter.RecoverAsync(
                DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key));

            Assert.Equal(unresolved, dispatched.Outcome);
            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovery.Outcome);
            Assert.Equal(dispatched.ProviderReference, recovery.ProviderReference);
            Assert.Equal(dispatched.CouponNumbers, recovery.CouponNumbers);
        }

        [Fact]
        public async Task A_resolved_refusal_is_never_rewritten_by_a_later_read_back()
        {
            var adapter = new DeterministicDocumentRefundAdapter
            {
                RefundOutcome = ProviderOperationOutcome.Rejected,
                RecoveryOutcome = ProviderOperationOutcome.Confirmed
            };

            await adapter.RefundAsync(DocumentRefundPortFixture.Request());

            var recovery = await adapter.RecoverAsync(
                DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key));

            Assert.True(recovery.WasDispatched);
            Assert.Equal(ProviderOperationOutcome.Rejected, recovery.Outcome);
            Assert.Null(recovery.ProviderReference);
        }

        [Fact]
        public async Task A_contradictory_echo_is_reported_verbatim_so_ordering_can_refuse_it()
        {
            var adapter = new DeterministicDocumentRefundAdapter
            {
                ReportedDocumentNumber = "M9399998",
                ReportedCouponNumbers = [9]
            };

            var result = await adapter.RefundAsync(DocumentRefundPortFixture.Request());

            Assert.Equal(ProviderOperationOutcome.Confirmed, result.Outcome);
            Assert.Equal("M9399998", result.DocumentNumber);
            Assert.Equal([9], result.CouponNumbers);
        }

        [Fact]
        public async Task An_unreachable_provider_is_never_read_as_a_negative_answer()
        {
            var adapter = new DeterministicDocumentRefundAdapter { ThrowOnRecover = true };

            await adapter.RefundAsync(DocumentRefundPortFixture.Request());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => adapter.RecoverAsync(DocumentRefundPortFixture.Recovery(DocumentRefundPortFixture.Key)));

            Assert.Single(adapter.DispatchedKeys);
        }
    }
}
