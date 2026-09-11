using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.Contracts.AncillaryDisposition.AncillaryDispositionPortFixture;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.AncillaryDisposition
{
    public abstract class AncillaryDispositionPortContract
    {
        protected abstract IAncillaryExchangeDispositionPort Port();

        protected abstract IAncillaryExchangeDispositionPort PortAnswering(AncillaryExchangeDisposition disposition);

        [Fact]
        public async Task Every_affected_coupon_receives_an_explicit_decision()
        {
            var request = Request();
            var result = await Port().DecideAsync(request);

            Assert.All(
                request.AffectedCoupons,
                coupon => Assert.Single(
                    result.Dispositions,
                    decision => decision.EmdDocumentNumber == coupon.EmdDocumentNumber
                                && decision.EmdCouponNumber == coupon.EmdCouponNumber));
        }

        [Fact]
        public async Task No_ancillary_outside_the_affected_scope_may_appear_in_the_answer()
        {
            var request = Request();
            var result = await Port().DecideAsync(request);

            var affected = request.AffectedCoupons.Select(coupon => Key(coupon.EmdDocumentNumber, coupon.EmdCouponNumber))
                .ToHashSet(StringComparer.Ordinal);

            Assert.All(
                result.Dispositions,
                decision => Assert.Contains(Key(decision.EmdDocumentNumber, decision.EmdCouponNumber), affected));
        }

        [Fact]
        public async Task A_coupon_is_never_decided_twice()
        {
            var result = await Port().DecideAsync(Request());

            var keys = result.Dispositions
                .Select(decision => Key(decision.EmdDocumentNumber, decision.EmdCouponNumber))
                .ToList();

            Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public async Task The_predecessor_binding_of_every_decision_is_exact()
        {
            var request = Request();
            var result = await Port().DecideAsync(request);

            Assert.All(result.Dispositions, decision =>
            {
                var affected = request.AffectedCoupons.Single(coupon =>
                    coupon.EmdDocumentNumber == decision.EmdDocumentNumber
                    && coupon.EmdCouponNumber == decision.EmdCouponNumber);

                Assert.Equal(affected.PredecessorDocumentNumber, decision.PredecessorDocumentNumber);
                Assert.Equal(affected.PredecessorCouponNumber, decision.PredecessorCouponNumber);
                Assert.Equal(request.PredecessorDocumentNumber, decision.PredecessorDocumentNumber);
            });
        }

        [Fact]
        public async Task A_reassociation_target_is_always_inside_the_reissue_scope()
        {
            var request = Request();
            var result = await Port().DecideAsync(request);

            Assert.All(
                result.Dispositions.Where(decision =>
                    decision.Disposition == AncillaryExchangeDisposition.ReassociateExisting),
                decision =>
                {
                    Assert.NotNull(decision.TargetPredecessorCouponNumber);
                    Assert.Contains(decision.TargetPredecessorCouponNumber!.Value, request.ReissueScopeCouponNumbers);
                });
        }

        [Fact]
        public async Task A_coupon_outside_the_reissue_scope_is_never_offered_as_a_target()
        {
            var request = Request();
            var result = await Port().DecideAsync(request);

            Assert.DoesNotContain(request.ReissueScopeCouponNumbers, number => number == FlownCouponNumber);
            Assert.All(
                result.Dispositions,
                decision => Assert.NotEqual(FlownCouponNumber, decision.TargetPredecessorCouponNumber));
        }

        [Fact]
        public async Task The_answer_is_bound_to_the_context_it_was_asked_about()
        {
            var request = Request();
            var result = await Port().DecideAsync(request);

            Assert.Equal(request.QuotedExchangeId, result.QuotedExchangeId);
            Assert.Equal(request.ContextFingerprint, result.ContextFingerprint);
            Assert.False(string.IsNullOrWhiteSpace(result.DecisionReference));
        }

        [Fact]
        public async Task An_answer_for_one_context_can_never_pass_as_an_answer_for_another()
        {
            var port = Port();
            var mine = Request();
            var other = Request(OtherQuotedExchangeId);

            var forMine = await port.DecideAsync(mine);
            var forOther = await port.DecideAsync(other);

            Assert.NotEqual(mine.ContextFingerprint, other.ContextFingerprint);
            Assert.NotEqual(forMine.ContextFingerprint, forOther.ContextFingerprint);
            Assert.NotEqual(forMine.QuotedExchangeId, forOther.QuotedExchangeId);
        }

        [Fact]
        public async Task A_different_predecessor_document_is_a_different_context()
        {
            var port = Port();
            var mine = Request();
            var other = Request(QuotedExchangeId, OtherPredecessorDocumentNumber);

            var forMine = await port.DecideAsync(mine);
            var forOther = await port.DecideAsync(other);

            Assert.NotEqual(mine.ContextFingerprint, other.ContextFingerprint);
            Assert.NotEqual(forMine.ContextFingerprint, forOther.ContextFingerprint);
        }

        [Fact]
        public async Task The_same_context_fingerprints_identically_however_often_it_is_asked()
        {
            var first = Request();
            var second = Request();

            Assert.Equal(first.ContextFingerprint, second.ContextFingerprint);
            Assert.Equal(first.ContextFingerprint, ExchangeAncillaryPlanner.Fingerprint(first));

            var answered = await Port().DecideAsync(first);

            Assert.Equal(first.ContextFingerprint, answered.ContextFingerprint);
        }

        [Theory]
        [InlineData(AncillaryExchangeDisposition.ReassociateExisting)]
        [InlineData(AncillaryExchangeDisposition.Refund)]
        [InlineData(AncillaryExchangeDisposition.ExchangeToNewEmd)]
        [InlineData(AncillaryExchangeDisposition.RetainAsResidual)]
        [InlineData(AncillaryExchangeDisposition.Cancel)]
        [InlineData(AncillaryExchangeDisposition.ManualReview)]
        public async Task Every_disposition_survives_the_port_without_being_remapped(
            AncillaryExchangeDisposition disposition)
        {
            var result = await PortAnswering(disposition).DecideAsync(SingleCouponRequest());
            var decided = Assert.Single(result.Dispositions);

            Assert.Equal(disposition, decided.Disposition);
            Assert.True(Enum.IsDefined(decided.Disposition));
        }
    }
}
