using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class AncillaryManualReviewFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly long _seed;
        private readonly string _manual;
        private readonly string _automated;

        public AncillaryManualReviewFlowTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;
            _seed = Random.Shared.NextInt64(10_000_000, 99_999_999);

            _manual = $"M5{_seed}";
            _automated = $"M7{_seed}";
        }

        // ---------------------------------------------- 22-24. a manual review is an outcome, not a failure

        [Fact]
        public async Task G5M1_a_manual_review_only_reissue_exchanges_the_ticket_and_ends_needing_reconciliation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _manual, [scenario.CouponId]);

            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ManualReview;

            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var document = await AncillaryAsync(_fixture, scenario.OrderId, _manual);
            var coupon = document.Coupons.Single();
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var manual = plan!.AncillaryManualReviews.Single();

            // 22. the ticket exchange is allowed to proceed and is complete truth
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);

            // 23. the manual-review coupon stays open and detached by this reissue, with no reassociation
            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.True(coupon.IsDisassociatedByReissue(outcome.OperationId));
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
            Assert.NotEqual(ElectronicMiscDocumentStatus.Voided, document.StatusSummary);
            Assert.Null(document.VoidRecord);

            // 14. the source evidence is durable and unambiguous
            Assert.Equal(AncillaryExchangeDisposition.ManualReview, manual.Disposition);
            Assert.Equal("the supplier cannot automate this ancillary", manual.ManualReviewReason);
            Assert.Equal("ANC-DISPOSITION", manual.DecisionReference);
            Assert.Equal(1, manual.DecisionVersion);
            Assert.False(string.IsNullOrWhiteSpace(manual.DecisionContextFingerprint));

            // the disposition is intentionally unresolved, never marked confirmed to force completion
            Assert.Null(manual.AssociationOutcome);
            Assert.False(manual.IsSettled);
            Assert.True(plan.HasUnresolvedManualReview);

            // 24. no provider act, no value movement, no commercial mutation
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentVoids.ObservedEligibilityRequests);
            Assert.Empty(harness.DocumentVoids.ObservedVoidRequests);
            Assert.Empty(plan.CancelGroups);
            Assert.Empty(plan.ExchangeGroups);

            AssertOnlyTheAirServiceMoved(before, after, scenario);
        }

        [Fact]
        public async Task G5M2_replaying_a_manual_review_operation_repeats_nothing()
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _manual, [scenario.CouponId]);

            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ManualReview;

            var key = NewKey();
            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var afterFirst = await ReloadAsync(_fixture, scenario.OrderId);
            var documentAfterFirst = await AncillaryAsync(_fixture, scenario.OrderId, _manual);

            await using var replay = new OrderSliceHarness(_fixture, caller);
            Register(replay, scenario);
            replay.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ManualReview;

            var second = await replay.Exchange.ExchangeAsync(scenario.Execution(key));

            var afterSecond = await ReloadAsync(_fixture, scenario.OrderId);
            var documentAfterSecond = await AncillaryAsync(_fixture, scenario.OrderId, _manual);
            var plan = await replay.ExchangePlans.FindAsync(second.OperationId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, second.OperationStatus);

            Assert.Empty(replay.DocumentExchanges.ObservedRequests);
            Assert.Empty(replay.EmdAssociations.ObservedRequests);
            Assert.Empty(replay.DocumentVoids.ObservedVoidRequests);

            Assert.Equal(afterFirst.CommercialVersion, afterSecond.CommercialVersion);
            Assert.Equal(documentAfterFirst.DocumentVersion, documentAfterSecond.DocumentVersion);
            Assert.Equal(
                "the supplier cannot automate this ancillary",
                plan!.AncillaryManualReviews.Single().ManualReviewReason);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        // ---------------------------------------------- 25. an unactionable manual review fails closed

        [Fact]
        public async Task G5M3_a_manual_review_with_no_reason_refuses_before_the_ticket()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _manual, [scenario.CouponId]);

            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ManualReview;
            harness.AncillaryDispositions.OmitManualReviewReason = true;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20322, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);

            await AssertNoIrreversibleWorkAsync(harness, scenario, _manual);
        }

        [Fact]
        public async Task G5M4_a_manual_review_carrying_a_monetary_consequence_refuses_before_the_ticket()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _manual, [scenario.CouponId]);

            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ManualReview;
            harness.AncillaryDispositions.ReportManualReviewWithRefundTerms = true;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20297, refusal.Code);

            await AssertNoIrreversibleWorkAsync(harness, scenario, _manual);
        }

        // ---------------------------------------------- 17. manual review dominates whole-document cancel scope

        [Fact]
        public async Task G5M5_a_manual_review_coupon_blocks_cancelling_the_rest_of_the_same_emd()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup, roundTrip: true);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var first = await AirCouponAsync(issued, 1);
            var second = await AirCouponAsync(issued, 2);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _manual, [(first.Id, lounge.Id), (second.Id, lounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1, 2]);

            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_manual, 1)] =
                AncillaryExchangeDisposition.Cancel;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_manual, 2)] =
                AncillaryExchangeDisposition.ManualReview;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            // voiding the document would terminate the manual-review coupon, so the shape is refused up front
            Assert.Equal(20321, refusal.Code);

            await AssertNoIrreversibleWorkAsync(harness, scenario, _manual);
        }

        // ---------------------------------------------- 27. manual review never starves later automation

        [Fact]
        public async Task G5M6_a_manual_review_sorting_first_still_lets_the_later_cancel_settle()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var airCoupon = await AirCouponAsync(issued, 1);
            var manualFirst = $"M0{_seed}";

            await AttachAncillaryAsync(_fixture, setup, issued.OrderId, manualFirst, [airCoupon.Id]);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _automated, [(airCoupon.Id, lounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);

            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(manualFirst, 1)] =
                AncillaryExchangeDisposition.ManualReview;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_automated, 1)] =
                AncillaryExchangeDisposition.Cancel;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var manualDocument = await AncillaryAsync(_fixture, scenario.OrderId, manualFirst);
            var cancelledDocument = await AncillaryAsync(_fixture, scenario.OrderId, _automated);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);

            // the later cancel is fully settled even though the manual review sorts first
            Assert.Equal(ElectronicMiscDocumentStatus.Voided, cancelledDocument.StatusSummary);
            Assert.NotNull(plan!.CancelGroups.Single().CancellationSettledAt);
            Assert.Equal(
                OrderServiceCommercialStatus.Cancelled,
                after.OrderServices.Single(service => service.Id == lounge.Id).CommercialStatus);

            // and the manual review is still untouched and unresolved
            Assert.Equal(EmdCouponStatus.OpenForUse, manualDocument.Coupons.Single().Status);
            Assert.Null(manualDocument.Coupons.Single().AssociatedTicketCouponId);
            Assert.True(plan.HasUnresolvedManualReview);
        }

        // ---------------------------------------------- 26. every automatable outcome beside a manual review

        [Fact]
        public async Task G5M7_every_automatable_disposition_settles_beside_an_unresolved_manual_review()
        {
            var reassociated = $"M1{_seed}";
            var refunded = $"M2{_seed}";
            var exchanged = $"M3{_seed}";
            var retained = $"M4{_seed}";
            var cancelled = $"M6{_seed}";
            var manual = $"M8{_seed}";

            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var airCoupon = await AirCouponAsync(issued, 1);

            foreach (var documentNumber in new[] { reassociated, refunded, exchanged, retained, manual })
                await AttachAncillaryAsync(_fixture, setup, issued.OrderId, documentNumber, [airCoupon.Id]);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, cancelled, [(airCoupon.Id, lounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);

            var dispositions = harness.AncillaryDispositions;

            dispositions.DispositionByCoupon[AncillaryKey(reassociated, 1)] =
                AncillaryExchangeDisposition.ReassociateExisting;
            dispositions.DispositionByCoupon[AncillaryKey(refunded, 1)] = AncillaryExchangeDisposition.Refund;
            dispositions.DispositionByCoupon[AncillaryKey(exchanged, 1)] =
                AncillaryExchangeDisposition.ExchangeToNewEmd;
            dispositions.DispositionByCoupon[AncillaryKey(retained, 1)] =
                AncillaryExchangeDisposition.RetainAsResidual;
            dispositions.DispositionByCoupon[AncillaryKey(cancelled, 1)] = AncillaryExchangeDisposition.Cancel;
            dispositions.DispositionByCoupon[AncillaryKey(manual, 1)] = AncillaryExchangeDisposition.ManualReview;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(6, plan!.Ancillaries.Count);

            // G1 reassociation settled against the successor ticket
            var reassociation = plan.Reassociations.Single();

            Assert.Equal(ProviderOperationOutcome.Confirmed, reassociation.AssociationOutcome);
            Assert.NotNull(
                (await AncillaryAsync(_fixture, scenario.OrderId, reassociated))
                    .Coupons.Single().AssociatedTicketCouponId);

            // G2 refund settled as document plus value movement
            var refund = plan.AncillaryRefunds.Single();

            Assert.True(refund.IsRefundSettled);
            Assert.Equal(
                EmdCouponStatus.Refunded,
                (await AncillaryAsync(_fixture, scenario.OrderId, refunded)).Coupons.Single().Status);

            // G3 exchange settled into a successor document
            var exchangeGroup = plan.ExchangeGroups.Single();

            Assert.True(exchangeGroup.IsSettled);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, exchanged)).Coupons.Single().Status);

            // G4 retention settled and left the source coupon open
            Assert.NotNull(plan.AncillaryRetentions.Single().RetentionSettledAt);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, retained)).Coupons.Single().Status);

            // G5 cancel settled as a whole-document void plus the service consequence
            var cancelGroup = plan.CancelGroups.Single();
            var cancelledDocument = await AncillaryAsync(_fixture, scenario.OrderId, cancelled);

            Assert.NotNull(cancelGroup.CancellationSettledAt);
            Assert.Equal(ElectronicMiscDocumentStatus.Voided, cancelledDocument.StatusSummary);
            Assert.Equal(
                OrderServiceCommercialStatus.Cancelled,
                after.OrderServices.Single(service => service.Id == lounge.Id).CommercialStatus);

            // and only the manual review is left, which is why the operation ends needing reconciliation
            var manualDocument = await AncillaryAsync(_fixture, scenario.OrderId, manual);

            Assert.True(plan.HasUnresolvedManualReview);
            Assert.Equal(EmdCouponStatus.OpenForUse, manualDocument.Coupons.Single().Status);
            Assert.Null(manualDocument.Coupons.Single().AssociatedTicketCouponId);
            Assert.True(manualDocument.Coupons.Single().IsDisassociatedByReissue(outcome.OperationId));
            Assert.Single(harness.DocumentVoids.ObservedVoidRequests);
        }

        // ---------------------------------------------- helpers

        private static void AssertOnlyTheAirServiceMoved(Order before, Order after, ExchangeScenario scenario)
        {
            foreach (var service in after.OrderServices.Where(candidate =>
                         candidate.ServiceType != OrderServiceType.AirTransportation))
            {
                var original = before.OrderServices.Single(candidate => candidate.Id == service.Id);

                Assert.Equal(original.Status, service.Status);
                Assert.Equal(original.CommercialStatus, service.CommercialStatus);
                Assert.Equal(original.DocumentStatus, service.DocumentStatus);
                Assert.Equal(original.FinancialStatus, service.FinancialStatus);
                Assert.Equal(original.DeliveryStatus, service.DeliveryStatus);
            }

            Assert.Equal(scenario.CommercialVersion + 1, after.CommercialVersion);
        }

        private async Task AssertNoIrreversibleWorkAsync(
            OrderSliceHarness harness,
            ExchangeScenario scenario,
            string documentNumber)
        {
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var document = await AncillaryAsync(_fixture, scenario.OrderId, documentNumber);

            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentVoids.ObservedVoidRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(predecessor.Exchanges);
            Assert.NotEqual(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.All(
                document.Coupons,
                coupon => Assert.DoesNotContain(
                    coupon.AssociationChanges,
                    change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue));
        }

        private async Task<IssuedTicket> LoungeOrderAsync(OrderSliceHarness harness, bool roundTrip = false)
            => await IssuedAsync(
                _fixture,
                harness,
                roundTrip,
                beforeReservation: async order =>
                {
                    order.AddProduct(
                        ProductAdditionFactory.Args(ProductAdditionFactory.EmdLounge(order)),
                        harness.Ids,
                        harness.Clock);

                    await harness.UnitOfWork.SaveChangesAsync();
                });

        private async Task<OrderService> LoungeServiceAsync(long orderId)
            => (await ReloadAsync(_fixture, orderId))
                .OrderServices.Single(service => service.ServiceType == OrderServiceType.LoungeAccess);

        private async Task<Domain.ElectronicTicketAggregate.Entities.TicketCoupon> AirCouponAsync(
            IssuedTicket issued,
            int couponNumber)
            => (await TicketAsync(_fixture, issued.OrderId, issued.TicketId))
                .Coupons.Single(coupon => coupon.CouponNumber == couponNumber);

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"ancmanual-{Guid.NewGuid():N}");
    }
}
