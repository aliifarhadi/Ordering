using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
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
    public sealed class PostDocumentTruthFreezeGateTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document = NewDocumentNumber();

        public PostDocumentTruthFreezeGateTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------- FG1, FG2. an unresolved collection hides nothing

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task FG1_FG2_an_unresolved_capture_leaves_the_reissue_authoritative_and_the_ancillary_detached(
            ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            await AttachToFirstCouponAsync(setup, scenario);
            harness.ExchangeFunding.CaptureOutcome = unresolved;
            harness.ExchangeFunding.CaptureRecoveryOutcome = unresolved;

            var held = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            await AssertReissueIsAuthoritativeAsync(scenario, held);
            await AssertAncillaryIsDetachedOnceAsync(scenario);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ExchangeFundingState.CapturePending, held.FundingState);
            Assert.Equal(ExchangeAncillaryState.NotStarted, held.AncillaryState);
            Assert.False(held.RequiresReconciliation);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);

            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Equal(held.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedCaptureRecoveryKeys);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            await AssertAncillaryIsDetachedOnceAsync(scenario);
        }

        // ---------------------------------------------- FG3. a refused collection reconciles, never rolls back

        [Fact]
        public async Task FG3_a_refused_capture_reconciles_without_undoing_the_reissue()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            await AttachToFirstCouponAsync(setup, scenario);
            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            await AssertReissueIsAuthoritativeAsync(scenario, outcome);
            await AssertAncillaryIsDetachedOnceAsync(scenario);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
        }

        // ---------------------------------------------- FG4, FG5. an unresolved return of value hides nothing

        [Theory]
        [InlineData(ChangeMonetaryOutcome.Refund)]
        [InlineData(ChangeMonetaryOutcome.Residual)]
        public async Task FG4_FG5_an_unresolved_return_of_value_leaves_the_reissue_authoritative(
            ChangeMonetaryOutcome monetaryOutcome)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await NegativeBalanceAsync(_fixture, setup, harness, monetaryOutcome, [1]);

            await AttachToFirstCouponAsync(setup, scenario);

            if (monetaryOutcome == ChangeMonetaryOutcome.Refund)
            {
                harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Pending;
                harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Pending;
            }
            else
            {
                harness.ExchangeResiduals.FulfillOutcome = ProviderOperationOutcome.Pending;
                harness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Pending;
            }

            var held = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            await AssertReissueIsAuthoritativeAsync(scenario, held);
            await AssertAncillaryIsDetachedOnceAsync(scenario);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ExchangeMonetaryState.Pending, held.MonetaryState);
            Assert.Equal(ExchangeAncillaryState.NotStarted, held.AncillaryState);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
        }

        // ---------------------------------------------- FG6, FG7. a mixed plan settles leg by leg

        [Theory]
        [InlineData(ExchangeMonetaryLegKind.RefundDue)]
        [InlineData(ExchangeMonetaryLegKind.Residual)]
        public async Task FG6_FG7_a_mixed_plan_holds_the_ancillary_until_its_second_leg_resolves(
            ExchangeMonetaryLegKind mixedReturn)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedAsync(_fixture, setup, harness, mixedReturn, [1]);
            var key = NewKey();

            await AttachToFirstCouponAsync(setup, scenario);

            if (mixedReturn == ExchangeMonetaryLegKind.RefundDue)
            {
                harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Pending;
                harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Pending;
            }
            else
            {
                harness.ExchangeResiduals.FulfillOutcome = ProviderOperationOutcome.Pending;
                harness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Pending;
            }

            var held = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            await AssertReissueIsAuthoritativeAsync(scenario, held);
            await AssertAncillaryIsDetachedOnceAsync(scenario);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ExchangeFundingState.Captured, held.FundingState);
            Assert.Equal(ExchangeMonetaryState.Pending, held.MonetaryState);
            Assert.Equal(ExchangeAncillaryState.NotStarted, held.AncillaryState);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);

            if (mixedReturn == ExchangeMonetaryLegKind.RefundDue)
                harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            else
                harness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var predecessorCouponId = (await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId))
                .Coupons.OrderBy(candidate => candidate.CouponNumber).First().Id;
            var successorCouponId = (await SuccessorAsync(scenario, finalized))
                .Coupons.Single(candidate => candidate.PredecessorTicketCouponId == predecessorCouponId).Id;
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(held.SuccessorElectronicTicketId, finalized.SuccessorElectronicTicketId);
            Assert.Equal(ExchangeAncillaryState.Confirmed, finalized.AncillaryState);
            Assert.Equal(successorCouponId, coupon.AssociatedTicketCouponId);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
            Assert.Single(
                (await ReloadAsync(_fixture, scenario.OrderId)).Changes,
                change => change.ChangeType == OrderChangeType.Exchange);
        }

        // ---------------------------------------------- D4. the local truth survives a crash before the money

        [Fact]
        public async Task D4_a_crash_before_the_first_capture_dispatch_keeps_the_reissue_and_resumes_the_money()
        {
            var caller = Caller();
            var funding = new DeterministicExchangeFundingAdapter { ThrowBeforeCaptureDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, exchangeFunding: funding);
            var scenario = await AddCollectAsync(_fixture, setup, crashed, [1]);
            var key = NewKey();

            await AttachToFirstCouponAsync(setup, scenario);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            await AssertAncillaryIsDetachedOnceAsync(scenario);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);
            var attempted = Assert.Single(funding.ObservedCaptures).OperationKey;

            Assert.DoesNotContain(attempted, funding.DispatchedKeys);

            funding.ThrowBeforeCaptureDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, exchangeFunding: funding);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Contains(attempted, funding.DispatchedKeys);
            Assert.All(funding.ObservedCaptures, request => Assert.Equal(attempted, request.OperationKey));
            await AssertAncillaryIsDetachedOnceAsync(scenario, reassociated: true);
        }

        // ---------------------------------------------- FG8. the frozen monetary paths keep their own sequencing

        [Fact]
        public async Task FG8_with_no_ancillary_the_frozen_collection_before_return_of_value_order_is_unchanged()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedAsync(_fixture, setup, harness, ExchangeMonetaryLegKind.RefundDue, [1]);

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Pending;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Pending;

            var held = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ExchangeFundingState.Captured, held.FundingState);
            Assert.Equal(ExchangeMonetaryState.Pending, held.MonetaryState);
            Assert.Equal(ExchangeAncillaryState.NotRequired, held.AncillaryState);
            Assert.Empty(held.Ancillaries);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedCaptures);
            Assert.NotEmpty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);

            await AssertReissueIsAuthoritativeAsync(scenario, held);
        }

        [Fact]
        public async Task FG8_a_collection_that_never_settles_never_returns_value()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await MixedAsync(_fixture, setup, harness, ExchangeMonetaryLegKind.RefundDue, [1]);

            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Pending;
            harness.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Pending;

            var held = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ExchangeFundingState.CapturePending, held.FundingState);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            await AssertReissueIsAuthoritativeAsync(scenario, held);
        }

        // ---------------------------------------------- FG10. revalidation is not a reissue

        [Fact]
        public async Task FG10_revalidation_leaves_the_ancillary_association_untouched()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var couponId = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).First().Id;

            await AttachAncillaryAsync(_fixture, setup, issued.OrderId, _document, [couponId]);

            var before = await AncillaryAsync(_fixture, issued.OrderId, _document);

            await using var harness = NewHarness();

            await RevalidationFixture.RevalidateAsync(_fixture, harness, issued, 1);

            var after = await AncillaryAsync(_fixture, issued.OrderId, _document);
            var coupon = after.Coupons.Single();

            Assert.Equal(couponId, coupon.AssociatedTicketCouponId);
            Assert.Equal(before.DocumentVersion, after.DocumentVersion);
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind != EmdCouponAssociationChangeKind.Associated);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
        }

        private async Task AssertReissueIsAuthoritativeAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
        {
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = await SuccessorAsync(scenario, outcome);

            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.False(string.IsNullOrWhiteSpace(outcome.SuccessorDocumentNumber));
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.Equal(ProviderOperationOutcome.Confirmed, outcome.DocumentExchangeOutcome);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.All(
                predecessor.Coupons.Where(coupon =>
                    scenario.ChangedOrderServiceIds.Contains(coupon.CurrentOrderServiceId)),
                coupon => Assert.Equal(TicketCouponFinancialStatus.Exchanged, coupon.FinancialStatus));
            Assert.Single(predecessor.Exchanges);

            Assert.Equal(predecessor.Id, successor.PredecessorElectronicTicketId);
            Assert.Equal(outcome.SuccessorDocumentNumber, successor.DocumentNumber);
            Assert.NotEmpty(successor.Coupons);
            Assert.Single(
                (await ReloadAsync(_fixture, scenario.OrderId)).Changes,
                change => change.ChangeType == OrderChangeType.Exchange);
            Assert.NotNull(outcome.OrderChangeId);
        }

        private async Task AssertAncillaryIsDetachedOnceAsync(ExchangeScenario scenario, bool reassociated = false)
        {
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);

            if (reassociated)
            {
                Assert.NotNull(coupon.AssociatedTicketCouponId);
                Assert.Single(
                    coupon.AssociationChanges,
                    change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);

                return;
            }

            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
        }

        private async Task<ElectronicMiscDocument> AttachToFirstCouponAsync(
            OrderSliceHarness setup,
            ExchangeScenario scenario)
        {
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            return await AttachAncillaryAsync(
                _fixture,
                setup,
                scenario.OrderId,
                _document,
                [ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).First().Id]);
        }

        private async Task<ElectronicTicket> SuccessorAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
            => (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId);

        private static string NewDocumentNumber() => $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7433, $"postdoc-{Guid.NewGuid():N}");
    }
}
