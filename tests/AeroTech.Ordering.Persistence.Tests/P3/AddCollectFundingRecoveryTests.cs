using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Commands.AcceptExchange;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class AddCollectFundingRecoveryTests
    {
        private const decimal AddCollect = ExchangeSourceFactory.AddCollectAmount;

        private readonly OrderingDatabaseFixture _fixture;

        public AddCollectFundingRecoveryTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- A. the real command path

        [Fact]
        public async Task A_the_funding_method_survives_the_command_boundary_all_the_way_to_the_provider()
        {
            const string fundingMethod = "FOP-COMMAND-PATH-1";

            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var handler = new AcceptExchangeCommandHandler(harness.Exchange);

            var outcome = await handler.Handle(
                new AcceptExchangeCommand(
                    scenario.OrderId,
                    scenario.ChangedOrderServiceIds,
                    ExchangeSourceFactory.QuoteId,
                    NewKey(),
                    scenario.CommercialVersion,
                    fundingMethod),
                CancellationToken.None);

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var guarantee = Assert.Single(harness.ExchangeFunding.ObservedGuarantees);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(fundingMethod, plan.FundingMethodRef);
            Assert.Equal(fundingMethod, guarantee.FundingMethodRef);
            Assert.Equal(AddCollect, guarantee.Amount);
            Assert.Equal(ExchangeFundingState.Captured, outcome.FundingState);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
        }

        [Fact]
        public async Task A_command_without_a_funding_method_is_refused_before_any_external_mutation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var handler = new AcceptExchangeCommandHandler(harness.Exchange);

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => handler.Handle(
                new AcceptExchangeCommand(
                    scenario.OrderId,
                    scenario.ChangedOrderServiceIds,
                    ExchangeSourceFactory.QuoteId,
                    NewKey(),
                    scenario.CommercialVersion),
                CancellationToken.None));

            Assert.Equal(20276, refusal.Code);
            Assert.Empty(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
        }

        // ---------------------------------------------------------------- B. contradictory guarantee evidence

        [Theory]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        [InlineData("missing-reference")]
        public async Task B_a_guarantee_that_contradicts_the_obligation_stops_before_inventory(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            switch (shape)
            {
                case "wrong-amount":
                    harness.ExchangeFunding.GuaranteeAmountOverride = AddCollect - 1m;
                    break;
                case "wrong-currency":
                    harness.ExchangeFunding.GuaranteeCurrencyOverride = scenario.Accepted.SaleCurrencyId + 7;
                    break;
                default:
                    harness.ExchangeFunding.OmitGuaranteeReference = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.False(string.IsNullOrWhiteSpace(plan.FundingGuaranteeDetail));
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
        }

        // ---------------------------------------------------------------- C, D. guarantee recovery

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task C_an_unresolved_guarantee_that_resolves_on_read_back_continues_the_workflow(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.GuaranteeOutcome = unresolved;
            harness.ExchangeFunding.GuaranteeRecoveryOutcome = unresolved;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);

            harness.ExchangeFunding.GuaranteeRecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var resumed = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Equal(first.OperationId, resumed.OperationId);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedGuaranteeRecoveryKeys);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Equal(ExchangeFundingState.Captured, resumed.FundingState);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task D_a_guarantee_that_stays_unresolved_is_never_guaranteed_again(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.GuaranteeOutcome = unresolved;
            harness.ExchangeFunding.GuaranteeRecoveryOutcome = unresolved;

            await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.False(replay.RequiresReconciliation);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedGuaranteeRecoveryKeys);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- E, F, G. release recovery

        [Fact]
        public async Task E_an_unresolved_release_holds_the_operation_until_it_is_read_back()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Rejected;
            harness.ExchangeFunding.ReleaseOutcome = ProviderOperationOutcome.Pending;
            harness.ExchangeFunding.ReleaseRecoveryOutcome = ProviderOperationOutcome.Pending;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ExchangeFundingState.ReleasePending, first.FundingState);
            Assert.False(first.RequiresReconciliation);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.ExchangeFunding.ReleaseRecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var settled = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;

            Assert.Equal(ServicingOperationStatus.Rejected, settled.OperationStatus);
            Assert.Equal(ExchangeFundingState.Released, settled.FundingState);
            Assert.Single(harness.ExchangeFunding.ObservedReleases);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedReleaseRecoveryKeys);
            Assert.True(plan.IsFundingReleased);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Null(settled.SuccessorElectronicTicketId);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task F_a_release_that_stays_unresolved_keeps_the_claim_and_is_never_released_again()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Rejected;
            harness.ExchangeFunding.ReleaseOutcome = ProviderOperationOutcome.Unknown;
            harness.ExchangeFunding.ReleaseRecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Equal(ExchangeFundingState.ReleasePending, replay.FundingState);
            Assert.Single(harness.ExchangeFunding.ObservedReleases);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedReleaseRecoveryKeys);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task G_a_release_confirmed_but_never_recorded_is_recovered_and_never_released_again()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Rejected;
            harness.ExchangeFunding.ThrowAfterReleaseDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            harness.ExchangeFunding.ThrowAfterReleaseDispatch = false;

            var settled = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.Rejected, settled.OperationStatus);
            Assert.Equal(ExchangeFundingState.Released, settled.FundingState);
            Assert.Single(harness.ExchangeFunding.ObservedReleases);
            Assert.Single(harness.ExchangeFunding.ObservedReleaseRecoveryKeys);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
        }

        [Fact]
        public async Task G_a_refused_release_needs_reconciliation_and_is_not_retried()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Rejected;
            harness.ExchangeFunding.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.True(replay.RequiresReconciliation);
            Assert.Single(harness.ExchangeFunding.ObservedReleases);
            Assert.True(plan.IsFundingReleaseRejected);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- H, I, J. capture recovery

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task H_I_an_unresolved_capture_that_resolves_on_read_back_finalizes_once(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.CaptureOutcome = unresolved;
            harness.ExchangeFunding.CaptureRecoveryOutcome = unresolved;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Null(first.SuccessorElectronicTicketId);

            harness.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(first.OperationId, finalized.OperationId);
            Assert.Equal(ExchangeFundingState.Captured, finalized.FundingState);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedCaptureRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(predecessor.Exchanges);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
            Assert.Equal(scenario.CustomerTotal + AddCollect, after.CustomerTotal);
        }

        [Fact]
        public async Task J_a_capture_that_stays_unresolved_is_read_back_and_never_captured_again()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Pending;
            harness.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Pending;

            await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var plan = (await harness.ExchangePlans.FindAsync(replay.OperationId))!;

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.False(replay.RequiresReconciliation);
            Assert.Equal(ExchangeFundingState.CapturePending, replay.FundingState);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedCaptureRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.Null(replay.SuccessorElectronicTicketId);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- K. a confirmed capture across a restart

        [Fact]
        public async Task K_a_capture_confirmed_in_the_provider_survives_a_process_restart_and_finalizes_once()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-funding-{Guid.NewGuid():N}");
            var provider = new DeterministicExchangeFundingAdapter { ThrowAfterCaptureDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, provider);
            var scenario = await AddCollectAsync(_fixture, setup, crashed, [1]);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.FundedExecution(key)));

            Assert.Single(provider.ObservedCaptures);
            Assert.DoesNotContain(
                await TicketsAsync(_fixture, scenario.OrderId),
                candidate => candidate.PredecessorElectronicTicketId is not null);

            provider.ThrowAfterCaptureDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, provider);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var recovery = Assert.Single(provider.ObservedCaptureRecoveryKeys);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(ExchangeFundingState.Captured, finalized.FundingState);
            Assert.Single(provider.ObservedCaptures);
            Assert.Equal(Assert.Single(provider.ObservedCaptures).OperationKey, recovery);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Empty(resumed.ReservationChanges.ObservedApplies);
            Assert.Empty(provider.ObservedReleases);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
            Assert.Equal(scenario.CustomerTotal + AddCollect, after.CustomerTotal);
        }

        // ---------------------------------------------------------------- L, M. capture evidence and refusal

        [Theory]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        [InlineData("missing-reference")]
        public async Task L_a_capture_that_contradicts_the_obligation_leaves_the_reissue_durable(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);

            switch (shape)
            {
                case "wrong-amount":
                    harness.ExchangeFunding.CaptureAmountOverride = AddCollect + 5m;
                    break;
                case "wrong-currency":
                    harness.ExchangeFunding.CaptureCurrencyOverride = scenario.Accepted.SaleCurrencyId + 7;
                    break;
                default:
                    harness.ExchangeFunding.OmitCaptureReference = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.NotNull(plan.Successor);
            Assert.False(string.IsNullOrWhiteSpace(plan.FundingCaptureDetail));
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(predecessor.Exchanges);
            Assert.Equal(ElectronicTicketStatus.Issued, predecessor.StatusSummary);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
        }

        [Fact]
        public async Task M_a_refused_capture_keeps_the_confirmed_reissue_and_needs_reconciliation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Rejected;

            var first = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Equal(ExchangeFundingState.CaptureRejected, replay.FundingState);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.True(plan.IsFundingCaptureRejected);
            Assert.NotNull(plan.Successor);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- O. partial use keeps every guarantee

        [Fact]
        public async Task O_a_partially_used_add_collect_recovers_its_capture_without_touching_used_history()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(
                _fixture, setup, harness, [2], [1],
                createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());
            var key = NewKey();

            harness.ExchangeFunding.CaptureOutcome = ProviderOperationOutcome.Unknown;
            harness.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Unknown;

            var unresolved = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, unresolved.OperationStatus);

            harness.ExchangeFunding.CaptureRecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(key));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, finalized.SuccessorElectronicTicketId!.Value))!;
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == used.Id);
            Assert.Equal(
                scenario.CouponServiceIds[2],
                Assert.Single(Assert.Single(harness.ReservationChanges.ObservedApplies).Items).ReplacedOrderServiceId);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"exc-funding-{Guid.NewGuid():N}"));
    }
}
