using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
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
    public sealed class AncillaryCancelFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document;
        private readonly string _secondDocument;

        public AncillaryCancelFlowTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;

            var seed = Random.Shared.NextInt64(10_000_000, 99_999_999);

            _document = $"M1{seed}";
            _secondDocument = $"M9{seed}";
        }

        // ---------------------------------------------- 1-8. the confirmed cancel and everything it must not touch

        [Fact]
        public async Task G5C1_a_single_coupon_service_emd_is_voided_and_its_ancillary_service_cancelled()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var airCoupon = await AirCouponAsync(issued, 1);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [(airCoupon.Id, lounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpCancel(harness);

            var before = await ReloadAsync(_fixture, scenario.OrderId);
            var beforeLounge = before.OrderServices.Single(service => service.Id == lounge.Id);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var cancelled = after.OrderServices.Single(service => service.Id == lounge.Id);
            var airService = after.OrderServices.Single(service => service.Id == airCoupon.CurrentOrderServiceId);
            var document = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();
            var exchangeSet = Assert.Single(
                after.PriceConsequencesOf(
                    after.Changes.Single(change => change.OperationId == outcome.OperationId).Id));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            // 3. the whole document and every coupon on it become void truth
            Assert.Equal(ElectronicMiscDocumentStatus.Voided, document.StatusSummary);
            Assert.Equal(EmdCouponStatus.Void, document.Coupons.Single().Status);
            Assert.Equal(outcome.OperationId, document.VoidRecord!.OperationId);
            Assert.Equal($"VOID-{_document}", document.VoidRecord.ProviderReference);

            // 3-5. the commercial consequence, and only the commercial consequence
            Assert.Equal(OrderServiceStatus.Cancelled, cancelled.Status);
            Assert.Equal(OrderServiceCommercialStatus.Cancelled, cancelled.CommercialStatus);
            Assert.Equal(OrderServiceDocumentStatus.Voided, cancelled.DocumentStatus);
            Assert.Equal(beforeLounge.FinancialStatus, cancelled.FinancialStatus);
            Assert.Equal(beforeLounge.DeliveryStatus, cancelled.DeliveryStatus);
            Assert.NotEqual(OrderServiceFinancialStatus.Refunded, cancelled.FinancialStatus);

            // 6. the air service keeps its frozen ticket-exchange truth
            Assert.Equal(OrderServiceCommercialStatus.Exchanged, airService.CommercialStatus);

            // 7. one price change set, the exchange's own, and exactly one G5 commercial version advance
            Assert.Equal(exchangeSet.ExpectedCommercialVersion + 2, after.CommercialVersion);
            Assert.Single(after.Changes.Where(change => change.OperationId == outcome.OperationId));

            // 8. no refund, residual or value movement anywhere
            Assert.Empty(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);

            // 2. exactly one eligibility check and one provider void for the document
            Assert.Equal(_document, Assert.Single(harness.DocumentVoids.ObservedVoidRequests).DocumentNumber);
            Assert.Single(harness.DocumentVoids.ObservedEligibilityRequests);
            Assert.Empty(harness.DocumentVoids.ObservedRecoveryRequests);
            Assert.Equal(
                AccountableDocumentKind.ElectronicMiscDocument,
                harness.DocumentVoids.ObservedVoidRequests[0].DocumentKind);
            Assert.Equal(
                OrderSliceHarness.HomeAirlineId,
                harness.DocumentVoids.ObservedVoidRequests[0].IssuerCarrierId);

            Assert.NotNull(group.CancellationSettledAt);
            Assert.Equal(ProviderOperationOutcome.Confirmed, group.VoidOutcome);
            Assert.Equal(EligibilityOutcome.Allowed, group.VoidEligibilityOutcome);
            Assert.Equal("ANC-CANCEL", group.CancellationReference);
            Assert.Equal("ANC-CANCEL-SOURCE", group.SourceReference);
            Assert.Equal(
                AncillaryCancellationDocumentAction.VoidWithoutRefund, group.DocumentAction);
            Assert.Equal([lounge.Id], group.OrderServiceIds);
        }

        [Fact]
        public async Task G5C2_a_multi_coupon_emd_cancelled_in_full_dispatches_one_provider_void()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await TwoLoungeOrderAsync(setup);
            var lounges = await LoungeServicesAsync(issued.OrderId);
            var first = await AirCouponAsync(issued, 1);
            var second = await AirCouponAsync(issued, 2);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document,
                [(first.Id, lounges[0].Id), (second.Id, lounges[1].Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1, 2]);
            SetUpCancel(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var document = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ElectronicMiscDocumentStatus.Voided, document.StatusSummary);
            Assert.All(document.Coupons, coupon => Assert.Equal(EmdCouponStatus.Void, coupon.Status));

            // one EMD, one provider void operation, one stable key
            Assert.Single(harness.DocumentVoids.ObservedVoidRequests);
            Assert.Single(harness.DocumentVoids.ObservedVoidKeys.Distinct());
            Assert.Equal([1, 2], group.EmdCouponNumbers);

            foreach (var lounge in lounges)
            {
                var cancelled = after.OrderServices.Single(service => service.Id == lounge.Id);

                Assert.Equal(OrderServiceStatus.Cancelled, cancelled.Status);
                Assert.Equal(OrderServiceCommercialStatus.Cancelled, cancelled.CommercialStatus);
                Assert.Equal(OrderServiceDocumentStatus.Voided, cancelled.DocumentStatus);
            }

            // one version move for one committed commercial consequence, not one per service
            Assert.Equal(scenario.CommercialVersion + 2, after.CommercialVersion);
        }

        [Fact]
        public async Task G5C5_a_delivered_or_no_show_ancillary_keeps_its_delivery_observation()
        {
            foreach (var observed in new[]
                     {
                         OrderServiceDeliveryStatus.Delivered,
                         OrderServiceDeliveryStatus.NoShow,
                         OrderServiceDeliveryStatus.Consumed
                     })
            {
                var documentNumber = $"{_document}{(int)observed}";

                await using var setup = NewHarness();
                await using var harness = NewHarness();

                var issued = await LoungeOrderAsync(setup);
                var lounge = await LoungeServiceAsync(issued.OrderId);
                var airCoupon = await AirCouponAsync(issued, 1);

                await AttachServiceAncillaryAsync(
                    _fixture, setup, issued.OrderId, documentNumber, [(airCoupon.Id, lounge.Id)]);

                await SetOrderServiceDeliveryStatusAsync(_fixture, lounge.Id, observed);

                var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
                SetUpCancel(harness);

                var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

                var after = await ReloadAsync(_fixture, scenario.OrderId);
                var cancelled = after.OrderServices.Single(service => service.Id == lounge.Id);

                Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
                Assert.Equal(observed, cancelled.DeliveryStatus);
                Assert.Equal(OrderServiceCommercialStatus.Cancelled, cancelled.CommercialStatus);
            }
        }

        // ---------------------------------------------- 9-11. eligibility outcomes

        [Fact]
        public async Task G5C9_pending_void_evidence_holds_the_operation_without_dispatching()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var context = await CancelContextAsync(setup, harness);

            harness.DocumentVoids.Eligibility = EligibilityOutcome.PendingEvidence;

            var outcome = await harness.Exchange.ExchangeAsync(context.Scenario.Execution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Equal(EligibilityOutcome.PendingEvidence, group.VoidEligibilityOutcome);
            Assert.Null(group.VoidDispatchedAt);
            Assert.Null(group.VoidOutcome);
            Assert.Null(group.CancellationSettledAt);
            Assert.Empty(harness.DocumentVoids.ObservedVoidRequests);

            await AssertTicketTruthHeldAsync(context, outcome);
            await AssertNothingCancelledAsync(context, unvoided: true);
        }

        [Fact]
        public async Task G5C10_denied_void_eligibility_reconciles_and_never_voids()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var context = await CancelContextAsync(setup, harness);

            harness.DocumentVoids.Eligibility = EligibilityOutcome.Denied;
            harness.DocumentVoids.EligibilityDetail = "the issuer refuses a void after departure";

            var outcome = await harness.Exchange.ExchangeAsync(context.Scenario.Execution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(EligibilityOutcome.Denied, group.VoidEligibilityOutcome);
            Assert.Equal("the issuer refuses a void after departure", group.VoidEligibilityDetail);
            Assert.Empty(harness.DocumentVoids.ObservedVoidRequests);

            await AssertTicketTruthHeldAsync(context, outcome);
            await AssertNothingCancelledAsync(context, unvoided: true);
        }

        [Fact]
        public async Task G5C11_a_refund_required_instead_answer_never_becomes_an_automatic_refund()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var context = await CancelContextAsync(setup, harness);

            harness.DocumentVoids.RefundRequiredInstead = true;

            var outcome = await harness.Exchange.ExchangeAsync(context.Scenario.Execution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(group.VoidRefundRequiredInstead);

            // the source approved cancel without refund; turning that into a refund is a new economic decision
            Assert.Empty(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.DocumentVoids.ObservedVoidRequests);
            Assert.Empty(plan.Ancillaries.Where(disposition => disposition.IsRefund));

            await AssertTicketTruthHeldAsync(context, outcome);
            await AssertNothingCancelledAsync(context, unvoided: true);
        }

        // ---------------------------------------------- 12-15. void execution outcomes

        [Fact]
        public async Task G5C12_a_pending_provider_void_holds_the_claim_and_never_writes_local_void()
            => await AssertUnresolvedVoidAsync(ProviderOperationOutcome.Pending);

        [Fact]
        public async Task G5C13_an_unknown_provider_void_holds_the_claim_and_never_writes_local_void()
            => await AssertUnresolvedVoidAsync(ProviderOperationOutcome.Unknown);

        [Fact]
        public async Task G5C14_a_rejected_provider_void_reconciles_and_leaves_the_emd_alone()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var context = await CancelContextAsync(setup, harness);

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(context.Scenario.Execution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Rejected, group.VoidOutcome);
            Assert.Null(group.CancellationSettledAt);

            await AssertTicketTruthHeldAsync(context, outcome);
            await AssertNothingCancelledAsync(context, unvoided: true);
        }

        [Fact]
        public async Task G5C15_a_confirmed_void_that_no_longer_matches_the_approved_group_never_voids_twice()
        {
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { ThrowAfterVoid = true };

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller, documentVoids: voids);

            var context = await CancelContextAsync(setup, harness);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(context.Scenario.Execution(key)));

            Assert.Single(voids.ObservedVoidRequests);

            // between the provider side effect and the local write, the coupon is refunded by another act
            var document = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);

            await SetAncillaryCouponStatusAsync(
                _fixture, document.Coupons.Single().Id, EmdCouponStatus.Refunded);

            voids.ThrowAfterVoid = false;
            voids.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(_fixture, caller, documentVoids: voids);
            Register(resumed, context.Scenario);

            var outcome = await resumed.Exchange.ExchangeAsync(context.Scenario.Execution(key));

            var plan = await resumed.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();
            var after = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);

            // the provider confirmation is retained, no second void is dispatched, no local void is fabricated
            Assert.Equal(ProviderOperationOutcome.Confirmed, group.VoidOutcome);
            Assert.Single(voids.ObservedVoidRequests);
            Assert.Null(group.CancellationSettledAt);
            Assert.NotEqual(ElectronicMiscDocumentStatus.Voided, after.StatusSummary);
            Assert.Null(after.VoidRecord);

            await AssertTicketTruthHeldAsync(context, outcome);
            await AssertServiceUntouchedAsync(context);
        }

        // ---------------------------------------------- 16-18. replay and crash boundaries

        [Fact]
        public async Task G5C16_replaying_a_completed_cancel_repeats_nothing()
        {
            var caller = Caller();

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller);

            var context = await CancelContextAsync(setup, harness);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(context.Scenario.Execution(key));
            var afterFirst = await ReloadAsync(_fixture, context.Scenario.OrderId);
            var voidedFirst = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);

            await using var replay = new OrderSliceHarness(_fixture, caller);
            Register(replay, context.Scenario);

            var second = await replay.Exchange.ExchangeAsync(context.Scenario.Execution(key));

            var afterSecond = await ReloadAsync(_fixture, context.Scenario.OrderId);
            var voidedSecond = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.Completed, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.Completed, second.OperationStatus);
            Assert.True(second.IsReplay);

            Assert.Equal(afterFirst.CommercialVersion, afterSecond.CommercialVersion);
            Assert.Equal(voidedFirst.DocumentVersion, voidedSecond.DocumentVersion);
            Assert.Equal(voidedFirst.VoidRecord!.VoidedAt, voidedSecond.VoidRecord!.VoidedAt);
            Assert.Empty(replay.DocumentVoids.ObservedVoidRequests);
            Assert.Empty(replay.DocumentVoids.ObservedRecoveryRequests);
        }

        [Fact]
        public async Task G5C17_a_crash_before_the_provider_void_never_voids_and_never_redispatches_blindly()
        {
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { ThrowBeforeVoid = true };

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller, documentVoids: voids);

            var context = await CancelContextAsync(setup, harness);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(context.Scenario.Execution(key)));

            var crashed = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);

            Assert.NotEqual(ElectronicMiscDocumentStatus.Voided, crashed.StatusSummary);
            Assert.Empty(voids.ObservedVoidRequests);

            voids.ThrowBeforeVoid = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, documentVoids: voids);
            Register(resumed, context.Scenario);

            var outcome = await resumed.Exchange.ExchangeAsync(context.Scenario.Execution(key));

            var plan = await resumed.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();
            var after = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);

            // the dispatch claim was durable before the call, so the resume reads back instead of re-voiding
            Assert.NotNull(group.VoidDispatchedAt);
            Assert.Empty(voids.ObservedVoidRequests);
            Assert.Single(voids.ObservedRecoveryRequests);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.NotEqual(ElectronicMiscDocumentStatus.Voided, after.StatusSummary);
        }

        [Fact]
        public async Task G5C18_a_crash_after_the_provider_void_recovers_without_a_duplicate_void()
        {
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { ThrowAfterVoid = true };

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller, documentVoids: voids);

            var context = await CancelContextAsync(setup, harness);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(context.Scenario.Execution(key)));

            Assert.Single(voids.ObservedVoidRequests);

            var crashed = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);

            Assert.NotEqual(ElectronicMiscDocumentStatus.Voided, crashed.StatusSummary);

            voids.ThrowAfterVoid = false;
            voids.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(_fixture, caller, documentVoids: voids);
            Register(resumed, context.Scenario);

            var outcome = await resumed.Exchange.ExchangeAsync(context.Scenario.Execution(key));

            var after = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);
            var order = await ReloadAsync(_fixture, context.Scenario.OrderId);
            var plan = await resumed.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(voids.ObservedVoidRequests);
            Assert.Single(voids.ObservedRecoveryRequests);
            Assert.Equal(ElectronicMiscDocumentStatus.Voided, after.StatusSummary);
            Assert.Single(after.Coupons.Where(coupon => coupon.Status == EmdCouponStatus.Void));
            Assert.NotNull(plan!.CancelGroups.Single().CancellationSettledAt);
            Assert.Equal(
                OrderServiceCommercialStatus.Cancelled,
                order.OrderServices.Single(service => service.Id == context.LoungeId).CommercialStatus);
        }

        // ---------------------------------------------- 19-21. the whole-document scope rule

        [Fact]
        public async Task G5C19_an_unaffected_open_coupon_on_the_same_emd_refuses_before_the_ticket()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await TwoLoungeOrderAsync(setup);
            var lounges = await LoungeServicesAsync(issued.OrderId);
            var first = await AirCouponAsync(issued, 1);
            var second = await AirCouponAsync(issued, 2);

            // coupon 2 hangs off a flown ticket coupon, so this reissue never makes it an affected ancillary
            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document,
                [(first.Id, lounges[0].Id), (second.Id, lounges[1].Id)]);

            await FlyCouponAsync(_fixture, issued.TicketId, second.Id);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpCancel(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20321, refusal.Code);
            Assert.Equal(409, refusal.HttpStatus);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        [Fact]
        public async Task G5C20_cancel_beside_refund_on_the_same_emd_refuses_before_the_ticket()
            => await AssertMixedSameDocumentRefusalAsync(AncillaryExchangeDisposition.Refund);

        [Fact]
        public async Task G5C21_cancel_beside_retention_on_the_same_emd_refuses_before_the_ticket()
            => await AssertMixedSameDocumentRefusalAsync(AncillaryExchangeDisposition.RetainAsResidual);

        [Fact]
        public async Task G5C21b_cancel_beside_reassociation_on_the_same_emd_refuses_before_the_ticket()
            => await AssertMixedSameDocumentRefusalAsync(AncillaryExchangeDisposition.ReassociateExisting);

        [Fact]
        public async Task G5C21c_an_already_terminal_coupon_on_the_same_emd_refuses_before_the_ticket()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await TwoLoungeOrderAsync(setup);
            var lounges = await LoungeServicesAsync(issued.OrderId);
            var first = await AirCouponAsync(issued, 1);
            var second = await AirCouponAsync(issued, 2);

            var document = await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document,
                [(first.Id, lounges[0].Id), (second.Id, lounges[1].Id)]);

            await SetAncillaryCouponStatusAsync(
                _fixture,
                document.Coupons.Single(coupon => coupon.CouponNumber == 2).Id,
                EmdCouponStatus.Refunded);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpCancel(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20321, refusal.Code);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        // ---------------------------------------------- pre-ticket term and target validation

        [Fact]
        public async Task G5C22_cancel_without_terms_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(20318, adapter => adapter.OmitCancellationTerms = true);

        [Fact]
        public async Task G5C23_cancel_with_a_blank_cancellation_reference_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(20318, adapter => adapter.OmitCancellationReference = true);

        [Fact]
        public async Task G5C24_cancel_with_a_blank_source_reference_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(20318, adapter => adapter.OmitCancellationSourceReference = true);

        [Fact]
        public async Task G5C25_cancel_with_an_unsupported_document_action_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(
                20318,
                adapter => adapter.CancellationDocumentAction = (AncillaryCancellationDocumentAction)99);

        [Fact]
        public async Task G5C26_cancel_that_also_carries_refund_terms_refuses_before_the_ticket()
            => await AssertPreTicketRefusalAsync(20297, adapter => adapter.ReportCancellationWithRefundTerms = true);

        [Fact]
        public async Task G5C27_cancel_on_a_non_service_coupon_refuses_before_the_ticket()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var scenario = await TicketedAsync(_fixture, harness);

            // the frozen fee-purpose attachment carries no order service at all
            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _document, [scenario.CouponId]);
            SetUpCancel(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20320, refusal.Code);
            Assert.Equal(409, refusal.HttpStatus);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        [Fact]
        public async Task G5C28_cancel_naming_a_foreign_order_service_refuses_before_the_ticket()
        {
            await using var setup = NewHarness();
            await using var foreign = NewHarness();
            await using var harness = NewHarness();

            var other = await LoungeOrderAsync(foreign);
            var foreignLounge = await LoungeServiceAsync(other.OrderId);

            var issued = await LoungeOrderAsync(setup);
            var airCoupon = await AirCouponAsync(issued, 1);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [(airCoupon.Id, foreignLounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpCancel(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20320, refusal.Code);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        [Fact]
        public async Task G5C29_cancel_naming_the_exchanged_air_service_refuses_before_the_ticket()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup);
            var airCoupon = await AirCouponAsync(issued, 1);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document,
                [(airCoupon.Id, airCoupon.CurrentOrderServiceId)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpCancel(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20320, refusal.Code);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        [Fact]
        public async Task G5C30_cancel_by_a_caller_with_no_identified_actor_refuses_before_the_ticket()
        {
            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(
                _fixture, TestCallerContexts.AirlineUser(7437, $"ancnoactor-{Guid.NewGuid():N}", airlineUserId: null));

            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var airCoupon = await AirCouponAsync(issued, 1);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [(airCoupon.Id, lounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpCancel(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20323, refusal.Code);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        // ---------------------------------------------- 28/30. post-ticket conflict and no invented statuses

        [Fact]
        public async Task G5C31_a_moved_association_after_the_ticket_reconciles_and_keeps_frozen_ticket_truth()
        {
            var caller = Caller();
            var associations = new DeterministicEmdAssociationAdapter();

            await using var setup = NewHarness();
            var issued = await TwoLoungeOrderAsync(setup);
            var lounge = (await LoungeServicesAsync(issued.OrderId))[0];
            var airCoupon = await AirCouponAsync(issued, 1);
            var untouched = await AirCouponAsync(issued, 2);

            // _document sorts first and takes the reassociation that crashes; _secondDocument carries the cancel
            await AttachAncillaryAsync(_fixture, setup, issued.OrderId, _document, [airCoupon.Id]);

            var cancelled = await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _secondDocument, [(airCoupon.Id, lounge.Id)]);

            await FlyCouponAsync(_fixture, issued.TicketId, untouched.Id);

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            var scenario = await QuotedAsync(_fixture, crashed, issued, [1]);
            ComposeReassociateThenCancel(crashed);
            var key = NewKey();

            associations.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var afterCrash = await ReloadAsync(_fixture, scenario.OrderId);

            // the cancel coupon moves onto a coupon this reissue never detached it from
            await AssociateAncillaryCouponAsync(_fixture, cancelled.Coupons.Single().Id, untouched.Id);

            associations.ThrowBeforeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            Register(resumed, scenario);
            ComposeReassociateThenCancel(resumed);

            var outcome = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var document = await AncillaryAsync(_fixture, scenario.OrderId, _secondDocument);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Empty(resumed.DocumentVoids.ObservedVoidRequests);
            Assert.NotEqual(ElectronicMiscDocumentStatus.Voided, document.StatusSummary);
            Assert.Equal(afterCrash.CommercialVersion, after.CommercialVersion);
            Assert.Equal(
                OrderServiceCommercialStatus.Active,
                after.OrderServices.Single(service => service.Id == lounge.Id).CommercialStatus);
        }

        [Fact]
        public async Task G5C32_a_confirmed_cancel_uses_only_the_frozen_void_statuses()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var context = await CancelContextAsync(setup, harness);

            await harness.Exchange.ExchangeAsync(context.Scenario.Execution(NewKey()));

            var document = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);
            var order = await ReloadAsync(_fixture, context.Scenario.OrderId);
            var cancelled = order.OrderServices.Single(service => service.Id == context.LoungeId);

            Assert.Equal(ElectronicMiscDocumentStatus.Voided, document.StatusSummary);
            Assert.All(document.Coupons, coupon => Assert.Equal(EmdCouponStatus.Void, coupon.Status));
            Assert.Contains(
                document.StatusSummary,
                Enum.GetValues<ElectronicMiscDocumentStatus>());
            Assert.Contains(OrderServiceDocumentStatus.Voided, Enum.GetValues<OrderServiceDocumentStatus>());
            Assert.Equal(OrderServiceDocumentStatus.Voided, cancelled.DocumentStatus);

            // the frozen coupon vocabulary carries no cancelled, forfeited or used terminal state
            Assert.DoesNotContain(
                Enum.GetNames<EmdCouponStatus>(),
                name => name is "Cancelled" or "Forfeited" or "Used");
        }

        [Fact]
        public async Task G5C33_a_service_conflict_after_a_confirmed_void_keeps_the_void_authoritative()
        {
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { ThrowAfterVoid = true };

            await using var setup = NewHarness();
            await using var harness = new OrderSliceHarness(_fixture, caller, documentVoids: voids);

            var context = await CancelContextAsync(setup, harness);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(context.Scenario.Execution(key)));

            // an unrelated act cancels the ancillary service before G5 ever applies its consequence
            await CancelOrderServiceAsync(_fixture, context.LoungeId);

            voids.ThrowAfterVoid = false;
            voids.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(_fixture, caller, documentVoids: voids);
            Register(resumed, context.Scenario);

            var outcome = await resumed.Exchange.ExchangeAsync(context.Scenario.Execution(key));

            var after = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);
            var order = await ReloadAsync(_fixture, context.Scenario.OrderId);
            var service = order.OrderServices.Single(candidate => candidate.Id == context.LoungeId);
            var plan = await resumed.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);

            // the EMD void truth stands, and is never rolled back for the service conflict
            Assert.Equal(ElectronicMiscDocumentStatus.Voided, after.StatusSummary);
            Assert.Equal(outcome.OperationId, after.VoidRecord!.OperationId);
            Assert.Single(voids.ObservedVoidRequests);
            Assert.Null(plan!.CancelGroups.Single().CancellationSettledAt);

            // and the service G5 did not cancel keeps whatever the other act left it as
            Assert.NotEqual(OrderServiceDocumentStatus.Voided, service.DocumentStatus);

            await AssertTicketTruthHeldAsync(context, outcome);
        }

        // ---------------------------------------------- shared assertions

        private async Task AssertUnresolvedVoidAsync(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var context = await CancelContextAsync(setup, harness);

            harness.DocumentVoids.VoidOutcome = unresolved;

            var outcome = await harness.Exchange.ExchangeAsync(context.Scenario.Execution(NewKey()));

            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);
            var group = plan!.CancelGroups.Single();

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Equal(unresolved, group.VoidOutcome);
            Assert.NotNull(group.VoidDispatchedAt);
            Assert.Null(group.CancellationSettledAt);
            Assert.Single(harness.DocumentVoids.ObservedVoidRequests);

            await AssertTicketTruthHeldAsync(context, outcome);
            await AssertNothingCancelledAsync(context, unvoided: true);
        }

        private async Task AssertMixedSameDocumentRefusalAsync(AncillaryExchangeDisposition other)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await TwoLoungeOrderAsync(setup);
            var lounges = await LoungeServicesAsync(issued.OrderId);
            var first = await AirCouponAsync(issued, 1);
            var second = await AirCouponAsync(issued, 2);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document,
                [(first.Id, lounges[0].Id), (second.Id, lounges[1].Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1, 2]);

            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 1)] =
                AncillaryExchangeDisposition.Cancel;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 2)] = other;
            harness.AncillaryDispositions.ExchangeSuccessorPurpose = EmdCouponPurpose.Service;
            harness.AncillaryDispositions.ExchangeSuccessorOrderServiceId = lounges[1].Id;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20321, refusal.Code);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        private async Task AssertPreTicketRefusalAsync(
            int expectedCode,
            Action<DeterministicAncillaryDispositionAdapter> malform)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var airCoupon = await AirCouponAsync(issued, 1);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [(airCoupon.Id, lounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpCancel(harness);
            malform(harness.AncillaryDispositions);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(expectedCode, refusal.Code);

            await AssertNoIrreversibleWorkAsync(harness, scenario);
        }

        private async Task AssertNoIrreversibleWorkAsync(OrderSliceHarness harness, ExchangeScenario scenario)
        {
            var predecessor = await PredecessorAsync(scenario);
            var document = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentVoids.ObservedEligibilityRequests);
            Assert.Empty(harness.DocumentVoids.ObservedVoidRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(predecessor.Exchanges);
            Assert.NotEqual(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.NotEqual(ElectronicMiscDocumentStatus.Voided, document.StatusSummary);
            Assert.All(
                document.Coupons,
                coupon => Assert.DoesNotContain(
                    coupon.AssociationChanges,
                    change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue));
        }

        private async Task AssertTicketTruthHeldAsync(CancelContext context, ExchangeOutcome outcome)
        {
            var predecessor = await PredecessorAsync(context.Scenario);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Single(
                await TicketsAsync(_fixture, context.Scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId == context.Scenario.TicketId);
        }

        private async Task AssertNothingCancelledAsync(CancelContext context, bool unvoided)
        {
            var document = await AncillaryAsync(_fixture, context.Scenario.OrderId, _document);

            if (unvoided)
            {
                Assert.NotEqual(ElectronicMiscDocumentStatus.Voided, document.StatusSummary);
                Assert.Null(document.VoidRecord);
                Assert.Equal(EmdCouponStatus.OpenForUse, document.Coupons.Single().Status);
            }

            await AssertServiceUntouchedAsync(context);
        }

        private async Task AssertServiceUntouchedAsync(CancelContext context)
        {
            var order = await ReloadAsync(_fixture, context.Scenario.OrderId);
            var lounge = order.OrderServices.Single(service => service.Id == context.LoungeId);

            Assert.Equal(OrderServiceCommercialStatus.Active, lounge.CommercialStatus);
            Assert.NotEqual(OrderServiceDocumentStatus.Voided, lounge.DocumentStatus);
        }

        // ---------------------------------------------- fixtures

        private async Task<CancelContext> CancelContextAsync(OrderSliceHarness setup, OrderSliceHarness harness)
        {
            var issued = await LoungeOrderAsync(setup);
            var lounge = await LoungeServiceAsync(issued.OrderId);
            var airCoupon = await AirCouponAsync(issued, 1);

            await AttachServiceAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [(airCoupon.Id, lounge.Id)]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            SetUpCancel(harness);

            return new CancelContext(scenario, lounge.Id);
        }

        private async Task<IssuedTicket> LoungeOrderAsync(OrderSliceHarness harness)
            => await IssuedAsync(
                _fixture,
                harness,
                beforeReservation: async order =>
                {
                    order.AddProduct(
                        ProductAdditionFactory.Args(ProductAdditionFactory.EmdLounge(order)),
                        harness.Ids,
                        harness.Clock);

                    await harness.UnitOfWork.SaveChangesAsync();
                });

        private async Task<IssuedTicket> TwoLoungeOrderAsync(OrderSliceHarness harness)
            => await IssuedAsync(
                _fixture,
                harness,
                roundTrip: true,
                beforeReservation: async order =>
                {
                    order.AddProduct(
                        ProductAdditionFactory.Args(ProductAdditionFactory.EmdLounge(order)),
                        harness.Ids,
                        harness.Clock);

                    order.AddProduct(
                        ProductAdditionFactory.Args(ProductAdditionFactory.EmdLounge(
                            order,
                            serviceRef: "LNG-EMD-2",
                            productRef: "PRODUCT-LNG-2",
                            quotedOfferId: "OFFER-LNG-2",
                            selectedOfferItemId: "OFFER-ITEM-LNG-2"),
                            operationId: ProductAdditionFactory.OperationId + 1),
                        harness.Ids,
                        harness.Clock);

                    await harness.UnitOfWork.SaveChangesAsync();
                });

        private async Task<OrderService> LoungeServiceAsync(long orderId)
            => (await ReloadAsync(_fixture, orderId))
                .OrderServices.Single(service => service.ServiceType == OrderServiceType.LoungeAccess);

        private async Task<IReadOnlyList<OrderService>> LoungeServicesAsync(long orderId)
            => (await ReloadAsync(_fixture, orderId))
                .OrderServices
                .Where(service => service.ServiceType == OrderServiceType.LoungeAccess)
                .OrderBy(service => service.Id)
                .ToList();

        private async Task<Domain.ElectronicTicketAggregate.Entities.TicketCoupon> AirCouponAsync(
            IssuedTicket issued,
            int couponNumber)
            => (await TicketAsync(_fixture, issued.OrderId, issued.TicketId))
                .Coupons.Single(coupon => coupon.CouponNumber == couponNumber);

        private static void SetUpCancel(OrderSliceHarness harness)
            => harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.Cancel;

        private void ComposeReassociateThenCancel(OrderSliceHarness harness)
        {
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 1)] =
                AncillaryExchangeDisposition.ReassociateExisting;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_secondDocument, 1)] =
                AncillaryExchangeDisposition.Cancel;
        }

        private async Task<ElectronicTicket> PredecessorAsync(ExchangeScenario scenario)
            => await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7437, $"anccancel-{Guid.NewGuid():N}");

        private sealed record CancelContext(ExchangeScenario Scenario, long LoungeId);
    }
}
