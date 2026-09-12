using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class RetainAsResidualFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document;
        private readonly string _secondDocument;

        public RetainAsResidualFlowTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;

            // AffectedAncillaries is ordered by document number, so the suffix fixes the settlement order.
            var seed = Random.Shared.NextInt64(10_000_000, 99_999_999);

            _document = $"M1{seed}";
            _secondDocument = $"M9{seed}";
        }

        // ---------------------------------------------- happy paths

        [Fact]
        public async Task G4H1_a_retained_ancillary_stays_open_detached_and_carries_durable_evidence()
        {
            await using var harness = NewHarness();
            var scenario = await RetentionScenarioAsync(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var coupon = ancillary.Coupons.Single();
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var retained = plan!.Ancillaries.Single();

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(AncillaryExchangeDisposition.RetainAsResidual, Assert.Single(outcome.Ancillaries).Disposition);

            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.Null(coupon.RefundRecord);
            Assert.Null(coupon.ExchangeRecord);
            Assert.Equal(ElectronicMiscDocumentStatus.Issued, ancillary.StatusSummary);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);

            Assert.Equal("ANC-RETENTION", retained.RetentionReference);
            Assert.Equal("ANC-RETENTION-SOURCE", retained.RetentionSourceReference);
            Assert.Equal(AncillaryRetentionMode.ExistingEmdCouponReusable, retained.RetentionMode);
            Assert.NotNull(retained.RetentionSettledAt);
            Assert.True(retained.IsRetentionSettled);

            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
        }

        [Fact]
        public async Task G4H3_a_retained_coupon_without_an_order_service_invents_none()
        {
            await using var harness = NewHarness();
            var scenario = await RetentionScenarioAsync(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.OperationId == outcome.OperationId);
            var exchangeSet = Assert.Single(after.PriceConsequencesOf(change.Id));

            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Null(coupon.OrderServiceId);

            // only the ticket exchange itself advanced the commercial version; retention added nothing
            Assert.Equal(exchangeSet.ExpectedCommercialVersion + 1, after.CommercialVersion);
        }

        [Fact]
        public async Task G4H4_reassociation_refund_exchange_and_retention_all_settle_independently()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var third = NewDocumentNumber();
            var fourth = NewDocumentNumber();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, third, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, fourth, [scenario.CouponId]);

            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 1)] =
                AncillaryExchangeDisposition.ReassociateExisting;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_secondDocument, 1)] =
                AncillaryExchangeDisposition.Refund;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(third, 1)] =
                AncillaryExchangeDisposition.ExchangeToNewEmd;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(fourth, 1)] =
                AncillaryExchangeDisposition.RetainAsResidual;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var moved = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var refunded = await AncillaryAsync(_fixture, scenario.OrderId, _secondDocument);
            var exchanged = await AncillaryAsync(_fixture, scenario.OrderId, third);
            var retained = await AncillaryAsync(_fixture, scenario.OrderId, fourth);
            var successorTicket = await SuccessorTicketAsync(scenario, outcome);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(4, outcome.Ancillaries.Count);

            Assert.Equal(successorTicket.Coupons.Single().Id, moved.Coupons.Single().AssociatedTicketCouponId);
            Assert.Equal(EmdCouponStatus.Refunded, refunded.Coupons.Single().Status);
            Assert.Equal(EmdCouponStatus.Exchanged, exchanged.Coupons.Single().Status);
            Assert.Equal(EmdCouponStatus.OpenForUse, retained.Coupons.Single().Status);
            Assert.Null(retained.Coupons.Single().AssociatedTicketCouponId);

            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
        }

        [Fact]
        public async Task G4H5_two_retained_coupons_each_get_evidence_and_an_unaffected_coupon_is_untouched()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var coupons = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).ToList();

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [coupons[0].Id, coupons[1].Id]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1, 2]);
            SetUpRetention(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(2, plan!.AncillaryRetentions.Count);
            Assert.All(plan.AncillaryRetentions, retained => Assert.True(retained.IsRetentionSettled));
            Assert.All(
                ancillary.Coupons,
                coupon => Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status));
            Assert.All(ancillary.Coupons, coupon => Assert.Null(coupon.AssociatedTicketCouponId));
            Assert.Equal(ElectronicMiscDocumentStatus.Issued, ancillary.StatusSummary);
        }

        [Fact]
        public async Task G4H6_a_coupon_outside_the_reissue_scope_is_never_retained()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var coupons = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).ToList();

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [coupons[0].Id, coupons[1].Id]);
            await FlyCouponAsync(_fixture, issued.TicketId, coupons[0].Id);

            var scenario = await QuotedAsync(_fixture, harness, issued, [2]);
            SetUpRetention(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var untouched = ancillary.Coupons.Single(coupon => coupon.CouponNumber == 1);
            var retained = ancillary.Coupons.Single(coupon => coupon.CouponNumber == 2);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(plan!.AncillaryRetentions);
            Assert.Equal(2, Assert.Single(plan.AncillaryRetentions).EmdCouponNumber);
            Assert.Equal(coupons[0].Id, untouched.AssociatedTicketCouponId);
            Assert.Null(retained.AssociatedTicketCouponId);
            Assert.Single(untouched.AssociationChanges);
        }

        // ---------------------------------------------- source validation before irreversible ticket work

        [Theory]
        [InlineData("no-terms", 20316)]
        [InlineData("blank-retention-reference", 20316)]
        [InlineData("blank-source-reference", 20316)]
        [InlineData("with-monetary-consequence", 20316)]
        [InlineData("new-miscellaneous-document", 20317)]
        [InlineData("voucher", 20317)]
        [InlineData("stored-value", 20317)]
        [InlineData("external-instrument", 20317)]
        [InlineData("credit-shell", 20317)]
        public async Task G4S1_an_unexecutable_retention_shape_fails_before_any_irreversible_work(
            string shape,
            int expectedCode)
        {
            await using var harness = NewHarness();
            var scenario = await RetentionScenarioAsync(harness);
            var dispositions = harness.AncillaryDispositions;

            switch (shape)
            {
                case "no-terms":
                    dispositions.OmitRetentionTerms = true;
                    break;
                case "blank-retention-reference":
                    dispositions.OmitRetentionReference = true;
                    break;
                case "blank-source-reference":
                    dispositions.OmitRetentionSourceReference = true;
                    break;
                case "with-monetary-consequence":
                    dispositions.ReportRetentionWithRefundTerms = true;
                    break;
                case "new-miscellaneous-document":
                    dispositions.RetentionMode = AncillaryRetentionMode.NewMiscellaneousDocument;
                    break;
                case "voucher":
                    dispositions.RetentionMode = AncillaryRetentionMode.Voucher;
                    break;
                case "stored-value":
                    dispositions.RetentionMode = AncillaryRetentionMode.StoredValue;
                    break;
                case "external-instrument":
                    dispositions.RetentionMode = AncillaryRetentionMode.ExternalInstrument;
                    break;
                default:
                    dispositions.RetentionMode = AncillaryRetentionMode.CreditShell;
                    break;
            }

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(expectedCode, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty((await PredecessorAsync(scenario)).Exchanges);
            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
            Assert.Equal(scenario.CouponId, coupon.AssociatedTicketCouponId);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
        }

        [Fact]
        public async Task G4S2_an_affected_ancillary_with_no_decision_still_fails_closed()
        {
            await using var harness = NewHarness();
            var scenario = await RetentionScenarioAsync(harness);

            harness.AncillaryDispositions.OmittedCoupons.Add(AncillaryKey(_document, 1));

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20296, refusal.Code);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty((await PredecessorAsync(scenario)).Exchanges);
        }

        // ---------------------------------------------- post-ticket conflicts

        [Theory]
        [InlineData(EmdCouponStatus.Refunded)]
        [InlineData(EmdCouponStatus.Exchanged)]
        [InlineData(EmdCouponStatus.Void)]
        public async Task G4C1_a_coupon_that_turned_terminal_after_acceptance_reconciles(EmdCouponStatus terminal)
        {
            var caller = Caller();
            var associations = new DeterministicEmdAssociationAdapter();

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            var scenario = await TwoCouponRetentionScenarioAsync(crashed);
            var key = NewKey();

            associations.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var document = await AncillaryAsync(_fixture, scenario.OrderId, _secondDocument);

            await SetAncillaryCouponStatusAsync(_fixture, document.Coupons.Single().Id, terminal);

            associations.ThrowBeforeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            Register(resumed, scenario);
            ComposeTwoCouponRetention(resumed);

            var outcome = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Empty(resumed.EmdExchanges.ObservedRequests);
            Assert.Empty(resumed.DocumentRefunds.ObservedRefundRequests);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        // ---------------------------------------------- replay and negative guarantees

        [Fact]
        public async Task G4R1_a_completed_retention_replays_without_a_second_consequence()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, setup);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var coupon = ticket.Coupons.First();

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [coupon.Id],
                deliveringOrderServiceId: coupon.CurrentOrderServiceId);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpRetention(harness);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var settledAt = (await harness.ExchangePlans.FindAsync(first.OperationId))!
                .Ancillaries.Single().RetentionSettledAt;

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var plan = await harness.ExchangePlans.FindAsync(first.OperationId);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.PriceChangeSets.Count, after.PriceChangeSets.Count);
            Assert.Equal(before.PricingLines.Count, after.PricingLines.Count);
            Assert.Equal(before.Changes.Count, after.Changes.Count);
            Assert.Equal(settledAt, plan!.Ancillaries.Single().RetentionSettledAt);
            Assert.Equal(2, ancillary.DocumentVersion);
            Assert.Single(
                ancillary.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
        }

        [Fact]
        public async Task G4R2_retention_moves_no_money_and_commits_no_pricing_consequence()
        {
            await using var harness = NewHarness();
            var scenario = await RetentionScenarioAsync(harness);

            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.OperationId == outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(after.PriceConsequencesOf(change.Id));
            Assert.Equal(
                before.PriceChangeSets.Count + 1,
                after.PriceChangeSets.Count);
            Assert.DoesNotContain(
                harness.Events.Dispatched.OfType<OrderPricingChanged>(),
                raised => raised.SourcePricingReference == "ANC-RETENTION-SOURCE");
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
        }

        [Fact]
        public async Task G4R3_a_crash_before_the_retention_checkpoint_settles_exactly_once_on_resume()
        {
            var caller = Caller();
            var associations = new DeterministicEmdAssociationAdapter();

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            var scenario = await TwoCouponRetentionScenarioAsync(crashed);
            var key = NewKey();

            associations.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Null(
                (await crashed.ExchangePlans.FindAsync(
                    (await ReloadAsync(_fixture, scenario.OrderId)).Changes
                        .Single(change => change.OperationId is not null).OperationId!.Value))!
                    .AncillaryRetentions.Single().RetentionSettledAt);

            associations.ThrowBeforeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            Register(resumed, scenario);
            ComposeTwoCouponRetention(resumed);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var plan = await resumed.ExchangePlans.FindAsync(finalized.OperationId);
            var retained = plan!.AncillaryRetentions.Single();
            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _secondDocument);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.True(replay.IsReplay);
            Assert.True(retained.IsRetentionSettled);
            Assert.Equal(EmdCouponStatus.OpenForUse, ancillary.Coupons.Single().Status);
            Assert.Null(ancillary.Coupons.Single().AssociatedTicketCouponId);
            Assert.Single(
                ancillary.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
        }

        // ---------------------------------------------- helpers

        private async Task<ExchangeScenario> RetentionScenarioAsync(OrderSliceHarness harness)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            SetUpRetention(harness);

            return scenario;
        }

        private async Task<ExchangeScenario> TwoCouponRetentionScenarioAsync(OrderSliceHarness harness)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            ComposeTwoCouponRetention(harness);

            return scenario;
        }

        private void ComposeTwoCouponRetention(OrderSliceHarness harness)
        {
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 1)] =
                AncillaryExchangeDisposition.ReassociateExisting;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_secondDocument, 1)] =
                AncillaryExchangeDisposition.RetainAsResidual;
        }

        private static void SetUpRetention(OrderSliceHarness harness)
            => harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.RetainAsResidual;

        private async Task<ElectronicTicket> PredecessorAsync(ExchangeScenario scenario)
            => await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

        private async Task<ElectronicTicket> SuccessorTicketAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
            => (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId);

        private static string NewDocumentNumber() => $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7436, $"ancret-{Guid.NewGuid():N}");
    }
}
