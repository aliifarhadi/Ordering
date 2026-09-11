using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Unconfigured;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class AddCollectExchangeFlowTests
    {
        private const decimal AddCollect = ExchangeSourceFactory.AddCollectAmount;
        private const string SecondQuoteId = "EXC-QUOTE-2";

        private readonly OrderingDatabaseFixture _fixture;

        public AddCollectExchangeFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- A. fully unused add collect

        [Fact]
        public async Task A_a_fully_unused_add_collect_reissue_funds_then_reissues_then_settles()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, changedCouponNumbers: [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var guarantee = Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            var capture = Assert.Single(harness.ExchangeFunding.ObservedCaptures);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ChangeMonetaryOutcome.AddCollect, outcome.MonetaryOutcome);
            Assert.Equal(AddCollect, outcome.AddCollectAmount);
            Assert.Equal(ExchangeFundingState.Captured, outcome.FundingState);
            Assert.False(outcome.RequiresReconciliation);

            Assert.Equal(AddCollect, guarantee.Amount);
            Assert.Equal(scenario.Accepted.SaleCurrencyId, guarantee.CurrencyId);
            Assert.Equal(ExchangeSourceFactory.FundingMethodRef, guarantee.FundingMethodRef);
            Assert.Equal(AddCollect, capture.Amount);
            Assert.Equal(successor.DocumentNumber, capture.SuccessorDocumentNumber);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);

            Assert.True(plan.IsFundingGuaranteed);
            Assert.True(plan.IsFundingCaptured);
            Assert.Equal(ExchangeFundingState.Captured, plan.FundingState);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.Single(outcome.Coupons, coupon => coupon.Disposition == ExchangeCouponDisposition.Replaced);
            Assert.Single(outcome.Coupons, coupon => coupon.Disposition == ExchangeCouponDisposition.Continued);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Equal(scenario.CustomerTotal + AddCollect, after.CustomerTotal);
            Assert.Equal(scenario.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence + 1, after.FinancialSequence);
            Assert.Equal(scenario.ObligationVersion + 1, after.ObligationVersion);
        }

        // ---------------------------------------------------------------- B. partially used add collect

        [Fact]
        public async Task B_a_partially_used_add_collect_reissue_keeps_used_coupons_out_of_every_mutation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [2], [1],
                createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies);
            var dispatch = Assert.Single(harness.DocumentExchanges.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeFundingState.Captured, outcome.FundingState);
            Assert.Equal(TicketCouponFinancialStatus.Used, predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1).FinancialStatus);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == scenario.CouponIds[1]);
            Assert.Equal(scenario.CouponServiceIds[2], Assert.Single(applied.Items).ReplacedOrderServiceId);
            Assert.Equal([2, 3], dispatch.Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.Equal(scenario.CustomerTotal + AddCollect, after.CustomerTotal);
        }

        // ---------------------------------------------------------------- C. the provider owns the money

        [Fact]
        public async Task C_the_provider_amount_and_currency_are_preserved_exactly()
        {
            const decimal unusualAmount = 317_411m;

            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], addCollectAmount: unusualAmount);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var penalty = Assert.Single(
                after.PricingLines,
                line => line.ComponentType == PricingComponentType.Penalty);

            Assert.Equal(unusualAmount, scenario.Accepted.AddCollect!.Amount);
            Assert.Equal(unusualAmount, plan.AddCollect!.Amount);
            Assert.Equal(unusualAmount, outcome.AddCollectAmount);
            Assert.Equal(scenario.Accepted.SaleCurrencyId, outcome.AddCollectCurrencyId);
            Assert.Equal(unusualAmount, Assert.Single(harness.ExchangeFunding.ObservedGuarantees).Amount);
            Assert.Equal(unusualAmount, Assert.Single(harness.ExchangeFunding.ObservedCaptures).Amount);
            Assert.Equal(unusualAmount, penalty.SaleAmount);
            Assert.Equal(scenario.CustomerTotal + unusualAmount, after.CustomerTotal);
            Assert.Equal(PricingSource.PricingEngine, plan.PricingSource);
            Assert.NotEqual(PricingSource.OrderingDerived, plan.PricingSource);
        }

        // ---------------------------------------------------------------- D. malformed money

        [Theory]
        [InlineData("no-amount")]
        [InlineData("zero")]
        [InlineData("negative")]
        [InlineData("foreign-currency")]
        [InlineData("contradicts-lines")]
        [InlineData("even-with-amount")]
        public async Task D_a_malformed_add_collect_is_refused_before_any_external_mutation(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1], shapeAccepted: accepted => shape switch
            {
                "no-amount" => accepted with { AddCollect = null },
                "zero" => accepted with { AddCollect = new AcceptedAddCollect(0m, accepted.SaleCurrencyId) },
                "negative" => accepted with { AddCollect = new AcceptedAddCollect(-AddCollect, accepted.SaleCurrencyId) },
                "foreign-currency" => accepted with { AddCollect = new AcceptedAddCollect(AddCollect, accepted.SaleCurrencyId + 7) },
                "contradicts-lines" => accepted with { AddCollect = new AcceptedAddCollect(AddCollect + 1m, accepted.SaleCurrencyId) },
                _ => accepted with
                {
                    MonetaryOutcome = ChangeMonetaryOutcome.Even,
                    PricingLines = [.. accepted.PricingLines.Where(line => line.ComponentType != PricingComponentType.Penalty)]
                }
            });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey())));

            Assert.Equal(20275, refusal.Code);
            await AssertNoExternalMutationAsync(harness, scenario, acceptCalls: 1);

            var plan = await harness.ExchangePlans.FindAsync(refusal.Data["OperationId"] is long id ? id : 0L);

            Assert.Null(plan);
        }

        [Fact]
        public async Task D_an_add_collect_without_a_funding_method_is_refused_before_any_external_mutation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey(), fundingMethodRef: null)));

            Assert.Equal(20276, refusal.Code);
            await AssertNoExternalMutationAsync(harness, scenario, acceptCalls: 1);
        }

        // ---------------------------------------------------------------- E. stale context

        [Fact]
        public async Task E_a_stale_expected_version_is_refused_before_any_external_mutation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(
                scenario.FundedExecution(NewKey()) with { ExpectedCommercialVersion = scenario.CommercialVersion + 5 }));

            Assert.Equal(20089, refusal.Code);
            await AssertNoExternalMutationAsync(harness, scenario, acceptCalls: 0);
        }

        // ---------------------------------------------------------------- F, G, H. the guarantee

        [Fact]
        public async Task F_a_refused_guarantee_stops_before_inventory_and_the_document()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            harness.ExchangeFunding.GuaranteeOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Equal(ExchangeFundingState.GuaranteeRejected, outcome.FundingState);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.True(plan.IsFundingGuaranteeRejected);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task G_an_unresolved_guarantee_is_read_back_and_never_guaranteed_again(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.GuaranteeOutcome = unresolved;
            harness.ExchangeFunding.GuaranteeRecoveryOutcome = unresolved;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Equal(ExchangeFundingState.GuaranteePending, replay.FundingState);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedGuaranteeRecoveryKeys);
            Assert.All(
                harness.ExchangeFunding.ObservedGuaranteeRecoveryKeys,
                recovered => Assert.Equal(Assert.Single(harness.ExchangeFunding.ObservedGuarantees).OperationKey, recovered));
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(unresolved, plan.FundingGuaranteeOutcome);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task H_a_guarantee_confirmed_but_never_recorded_is_recovered_and_never_charged_twice()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.ThrowAfterGuaranteeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            harness.ExchangeFunding.ThrowAfterGuaranteeDispatch = false;

            var resumed = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ExchangeFunding.ObservedGuaranteeRecoveryKeys);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Equal(ExchangeFundingState.Captured, resumed.FundingState);
        }

        // ---------------------------------------------------------------- I, J, K. inventory after funding

        [Fact]
        public async Task I_a_refused_reservation_releases_the_guarantee_it_no_longer_needs()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var release = Assert.Single(harness.ExchangeFunding.ObservedReleases);
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Equal(ExchangeFundingReleaseReason.ReservationRejected, release.Reason);
            Assert.Equal(plan.FundingGuaranteeReference, release.GuaranteeReference);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.True(plan.IsFundingReleased);
            Assert.Equal(ExchangeFundingState.Released, plan.FundingState);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Null(outcome.SuccessorElectronicTicketId);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task J_an_unresolved_reservation_never_repeats_the_funding_operation(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = unresolved;
            harness.ReservationChanges.RecoveryOutcome = unresolved;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.NotEmpty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ExchangeFundingState.Guaranteed, replay.FundingState);
        }

        [Fact]
        public async Task K_a_reservation_confirmed_but_never_recorded_continues_without_redispatching()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ReservationChanges.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            harness.ReservationChanges.ThrowAfterDispatch = false;
            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var resumed = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
        }

        // ---------------------------------------------------------------- L, M, N. the document

        [Fact]
        public async Task L_a_refused_reissue_releases_the_guarantee_and_needs_reconciliation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            harness.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var release = Assert.Single(harness.ExchangeFunding.ObservedReleases);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.Equal(ExchangeFundingReleaseReason.DocumentRejected, release.Reason);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.True(plan.IsFundingReleased);
            Assert.Equal(ExchangeFundingState.Released, plan.FundingState);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task M_an_unresolved_reissue_preserves_the_guarantee_and_the_claim(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.DocumentExchanges.ExchangeOutcome = unresolved;
            harness.DocumentExchanges.RecoveryOutcome = unresolved;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.NotEmpty(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.True(plan.IsFundingGuaranteed);
            Assert.False(plan.IsFundingCaptured);
            Assert.False(plan.IsFundingReleased);
            Assert.Equal(ExchangeFundingState.Guaranteed, replay.FundingState);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task N_a_reissue_confirmed_but_never_recorded_captures_once_and_never_reissues_again()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.DocumentExchanges.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            harness.DocumentExchanges.ThrowAfterDispatch = false;
            harness.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var resumed = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.NotNull(resumed.SuccessorElectronicTicketId);
        }

        // ---------------------------------------------------------------- O, P. money after an authoritative reissue

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task O_an_unresolved_capture_after_a_confirmed_reissue_stays_recoverable(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            harness.ExchangeFunding.CaptureOutcome = unresolved;
            harness.ExchangeFunding.CaptureRecoveryOutcome = unresolved;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.False(outcome.RequiresReconciliation);
            Assert.Equal(ExchangeFundingState.CapturePending, outcome.FundingState);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.NotNull(plan.Successor);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.NotNull(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(scenario.CustomerTotal + AddCollect, after.CustomerTotal);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task P_a_refused_capture_after_a_confirmed_reissue_records_the_economic_exception()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ExchangeFundingState.CaptureRejected, outcome.FundingState);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.True(plan.IsFundingCaptureRejected);
            Assert.NotNull(plan.Successor);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(predecessor.Exchanges);
            Assert.NotNull(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
        }

        // ---------------------------------------------------------------- Q. completed replay

        [Fact]
        public async Task Q_a_completed_add_collect_replay_moves_no_money_and_creates_nothing()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var before = await ReloadAsync(_fixture, scenario.OrderId);

            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            Assert.True(replay.IsReplay);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == scenario.TicketId);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(3, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
        }

        // ---------------------------------------------------------------- R, S. lineage

        [Fact]
        public async Task R_a_repeated_reissue_collects_against_the_current_accountable_predecessor()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));
            var successorId = first.SuccessorElectronicTicketId!.Value;
            var successor = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reloaded = await ReloadAsync(_fixture, scenario.OrderId);
            IReadOnlyList<long> secondChanged = [successor.Coupons.First().CurrentOrderServiceId];

            ComposeAddCollect(harness, reloaded, secondChanged, AddCollect, SecondQuoteId);

            await harness.Exchange.QuoteAsync(scenario.OrderId, secondChanged);

            var second = await harness.Exchange.ExchangeAsync(new ExchangeExecution(
                scenario.OrderId, secondChanged, SecondQuoteId, NewKey(), reloaded.CommercialVersion,
                ExchangeSourceFactory.FundingMethodRef));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var settledSuccessor = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reissued = (await FindTicketAsync(_fixture, second.SuccessorElectronicTicketId!.Value))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var guarantee = harness.ExchangeFunding.ObservedGuarantees;

            Assert.Equal(ServicingOperationStatus.Completed, second.OperationStatus);
            Assert.Equal(successorId, reissued.PredecessorElectronicTicketId);
            Assert.Equal(scenario.TicketId, settledSuccessor.PredecessorElectronicTicketId);
            Assert.Equal(2, guarantee.Count);
            Assert.Equal(settledSuccessor.DocumentNumber, guarantee[1].PredecessorDocumentNumber);
            Assert.Equal(predecessor.DocumentNumber, guarantee[0].PredecessorDocumentNumber);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(settledSuccessor.Exchanges);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(scenario.CustomerTotal + AddCollect + AddCollect, after.CustomerTotal);
        }

        [Fact]
        public async Task S_a_partially_used_add_collect_keeps_the_immediate_predecessor_lineage()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [2], [1],
                createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(scenario.TicketId, successor.PredecessorElectronicTicketId);
            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == used.Id);
            Assert.DoesNotContain(
                Assert.Single(predecessor.Exchanges).Coupons,
                coupon => coupon.PredecessorTicketCouponId == used.Id);
            Assert.Equal(
                [2, 3],
                successor.Coupons
                    .Select(coupon => predecessor.Coupons.Single(candidate => candidate.Id == coupon.PredecessorTicketCouponId).CouponNumber)
                    .Order());
        }

        // ---------------------------------------------------------------- V. the production adapter

        [Fact]
        public async Task V_an_unconfigured_funding_provider_fails_closed_on_every_stage()
        {
            IExchangeFundingPort unconfigured = new UnconfiguredExchangeFundingProvider();
            var recovery = new ExchangeFundingRecoveryRequest("exchange-funding-guarantee:1:1", 1, 1);

            var guarantee = await Assert.ThrowsAsync<BusinessException>(() => unconfigured.GuaranteeAsync(
                new ExchangeFundingGuaranteeRequest("k", 1, 1, "q", "T1", 1, AddCollect, 1, "fop")));
            var capture = await Assert.ThrowsAsync<BusinessException>(() => unconfigured.CaptureAsync(
                new ExchangeFundingCaptureRequest("k", 1, 1, "q", "T2", null, AddCollect, 1)));
            var release = await Assert.ThrowsAsync<BusinessException>(() => unconfigured.ReleaseAsync(
                new ExchangeFundingReleaseRequest("k", 1, 1, "q", null, ExchangeFundingReleaseReason.DocumentRejected)));

            await Assert.ThrowsAsync<BusinessException>(() => unconfigured.RecoverGuaranteeAsync(recovery));
            await Assert.ThrowsAsync<BusinessException>(() => unconfigured.RecoverCaptureAsync(recovery));
            await Assert.ThrowsAsync<BusinessException>(() => unconfigured.RecoverReleaseAsync(recovery));

            Assert.Equal(20263, guarantee.Code);
            Assert.Equal(20263, capture.Code);
            Assert.Equal(20263, release.Code);
            Assert.Equal(501, guarantee.HttpStatus);
        }

        // ---------------------------------------------------------------- W, X. observability and compatibility

        [Fact]
        public async Task W_an_unresolved_add_collect_exposes_provider_neutral_servicing_state()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Unknown;
            harness.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Unknown;

            var unresolved = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ChangeMonetaryOutcome.AddCollect, unresolved.MonetaryOutcome);
            Assert.Equal(AddCollect, unresolved.AddCollectAmount);
            Assert.Equal(scenario.Accepted.SaleCurrencyId, unresolved.AddCollectCurrencyId);
            Assert.Equal(ExchangeFundingState.CapturePending, unresolved.FundingState);
            Assert.Equal(ProviderOperationOutcome.Confirmed, unresolved.DocumentExchangeOutcome);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, unresolved.DocumentOutcome);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, unresolved.OperationStatus);
            Assert.False(unresolved.RequiresReconciliation);
            Assert.Equal(scenario.TicketId, unresolved.PredecessorElectronicTicketId);
            Assert.False(string.IsNullOrWhiteSpace(unresolved.SuccessorDocumentNumber));
            Assert.False(string.IsNullOrWhiteSpace(unresolved.FundingProviderReference));
            Assert.DoesNotContain(ExchangeSourceFactory.FundingMethodRef, unresolved.FundingProviderReference);

            await using var refused = NewHarness();
            var refusedScenario = await AddCollectAsync(_fixture, NewHarness(), refused, [1]);

            refused.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Rejected;

            var rejected = await refused.Exchange.ExchangeAsync(refusedScenario.FundedExecution(NewKey()));

            Assert.Equal(ExchangeFundingState.CaptureRejected, rejected.FundingState);
            Assert.Equal(ProviderOperationOutcome.Confirmed, rejected.DocumentExchangeOutcome);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, rejected.OperationStatus);
            Assert.True(rejected.RequiresReconciliation);
            Assert.NotEqual(unresolved.FundingState, rejected.FundingState);
            Assert.NotEqual(unresolved.OperationStatus, rejected.OperationStatus);
        }

        [Fact]
        public async Task X_an_even_reissue_still_needs_no_funding_at_all()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await PartiallyUsedAsync(_fixture, setup, harness, [1], [2]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ChangeMonetaryOutcome.Even, outcome.MonetaryOutcome);
            Assert.Null(outcome.AddCollectAmount);
            Assert.Null(outcome.AddCollectCurrencyId);
            Assert.Equal(ExchangeFundingState.NotRequired, outcome.FundingState);
            Assert.False(outcome.RequiresReconciliation);
            Assert.Null(plan.AddCollect);
            Assert.False(plan.RequiresFunding);
            Assert.Empty(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Equal(scenario.ObligationVersion, after.ObligationVersion);
        }

        // ---------------------------------------------------------------- support

        private async Task AssertNoExternalMutationAsync(
            OrderSliceHarness harness,
            ExchangeScenario scenario,
            int acceptCalls)
        {
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(acceptCalls, harness.ExchangeQuotes.ObservedSelections.Count);
            Assert.Empty(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.ObligationVersion, after.ObligationVersion);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"exc-addcollect-{Guid.NewGuid():N}"));
    }
}
