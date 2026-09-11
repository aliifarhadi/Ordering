using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class RefundDueExchangeFlowTests
    {
        private const decimal Owed = ExchangeSourceFactory.NegativeBalanceAmount;

        private readonly OrderingDatabaseFixture _fixture;

        public RefundDueExchangeFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- A, B. happy paths

        [Fact]
        public async Task A_a_fully_unused_refund_due_reissue_reissues_then_returns_the_value()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var refund = Assert.Single(harness.RefundValues.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ChangeMonetaryOutcome.Refund, outcome.MonetaryOutcome);
            Assert.Equal(Owed, outcome.MonetaryAmount);
            Assert.Equal(ExchangeMonetaryState.Settled, outcome.MonetaryState);
            Assert.Equal(AcceptedRefundDue.OriginalFormOfPayment, outcome.MonetaryDisposition);
            Assert.False(outcome.RequiresReconciliation);
            Assert.Null(outcome.ResidualInstrumentReference);

            Assert.Equal(Owed, refund.ApprovedAmount);
            Assert.Equal(AcceptedRefundDue.OriginalFormOfPayment, refund.ApprovedDisposition);
            Assert.Equal(predecessor.DocumentNumber, refund.DocumentNumber);
            Assert.Equal(successor.DocumentNumber, refund.SuccessorDocumentNumber);
            Assert.True(plan.IsRefundDueSettled);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
            Assert.Equal(scenario.CustomerTotal - Owed, after.CustomerTotal);
            Assert.Equal(scenario.CommercialVersion + 1, after.CommercialVersion);
        }

        [Fact]
        public async Task B_a_partially_used_refund_due_reissue_keeps_used_coupons_historical()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(
                setup, harness, [2], [1], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeMonetaryState.Settled, outcome.MonetaryState);
            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == used.Id);
            Assert.Equal(
                scenario.CouponServiceIds[2],
                Assert.Single(Assert.Single(harness.ReservationChanges.ObservedApplies).Items).ReplacedOrderServiceId);
            Assert.Equal([2, 3], Assert.Single(harness.DocumentExchanges.ObservedRequests).Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.Single(harness.RefundValues.ObservedRequests);
        }

        // ---------------------------------------------------------------- C. the provider owns the money

        [Fact]
        public async Task C_the_provider_refund_amount_is_preserved_exactly_end_to_end()
        {
            const decimal unusual = 143_977m;

            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1], settlementAmount: unusual);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(unusual, scenario.Accepted.RefundDue!.Amount);
            Assert.Equal(unusual, plan.RefundDue!.Amount);
            Assert.Equal(unusual, Assert.Single(harness.RefundValues.ObservedRequests).ApprovedAmount);
            Assert.Equal(unusual, outcome.MonetaryAmount);
            Assert.Equal(scenario.Accepted.SaleCurrencyId, outcome.MonetaryCurrencyId);
            Assert.Equal(scenario.CustomerTotal - unusual, after.CustomerTotal);
            Assert.Equal(PricingSource.PricingEngine, plan.PricingSource);
            Assert.NotEqual(PricingSource.OrderingDerived, plan.PricingSource);
        }

        // ---------------------------------------------------------------- D, E. refused before irreversible work

        [Theory]
        [InlineData("no-evidence")]
        [InlineData("zero")]
        [InlineData("negative")]
        [InlineData("wrong-currency")]
        [InlineData("outcome-contradiction")]
        [InlineData("balance-contradiction")]
        public async Task D_malformed_refund_pricing_is_refused_before_any_external_mutation(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1], shapeAccepted: accepted => shape switch
            {
                "no-evidence" => accepted with { RefundDue = null },
                "zero" => accepted with { RefundDue = Owing(accepted, 0m) },
                "negative" => accepted with { RefundDue = Owing(accepted, -Owed) },
                "wrong-currency" => accepted with
                {
                    RefundDue = new AcceptedRefundDue(Owed, accepted.SaleCurrencyId + 7, AcceptedRefundDue.OriginalFormOfPayment)
                },
                "outcome-contradiction" => accepted with
                {
                    MonetaryOutcome = ChangeMonetaryOutcome.Even,
                    PricingLines = [.. accepted.PricingLines.Where(line => line.LineRole != PricingLineRole.Adjustment)]
                },
                _ => accepted with { RefundDue = Owing(accepted, Owed + 1m) }
            });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20275, refusal.Code);
            await AssertNothingExternalAsync(harness, scenario, acceptCalls: 1);
        }

        [Fact]
        public async Task D_a_refund_disposition_that_is_not_the_original_source_is_refused()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1], shapeAccepted: accepted => accepted with
            {
                RefundDue = new AcceptedRefundDue(Owed, accepted.SaleCurrencyId, "SomeOtherDestination")
            });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20275, refusal.Code);
            await AssertNothingExternalAsync(harness, scenario, acceptCalls: 1);
        }

        [Fact]
        public async Task E_a_stale_expected_version_is_refused_before_any_external_mutation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => harness.Exchange.ExchangeAsync(
                scenario.Execution(NewKey()) with { ExpectedCommercialVersion = scenario.CommercialVersion + 5 }));

            Assert.Equal(20089, refusal.Code);
            await AssertNothingExternalAsync(harness, scenario, acceptCalls: 0);
        }

        // ---------------------------------------------------------------- F, R. no money before the document

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task F_R_an_unresolved_reissue_never_returns_value(ProviderOperationOutcome unresolved)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.DocumentExchanges.ExchangeOutcome = unresolved;
            harness.DocumentExchanges.RecoveryOutcome = unresolved;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.RefundValues.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ExchangeMonetaryState.Required, replay.MonetaryState);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task R_the_refund_is_dispatched_only_after_the_document_is_confirmed()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);

            harness.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Null(outcome.SuccessorElectronicTicketId);
        }

        // ---------------------------------------------------------------- G, H, I. refund recovery

        [Fact]
        public async Task G_a_confirmed_reissue_and_a_confirmed_refund_complete_the_exchange()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Confirmed, outcome.DocumentExchangeOutcome);
            Assert.Equal(ExchangeMonetaryState.Settled, outcome.MonetaryState);
            Assert.Single(harness.RefundValues.ObservedRequests);
        }

        [Fact]
        public async Task H_an_unresolved_refund_that_resolves_on_read_back_finalizes_once()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Pending;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Pending;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ExchangeMonetaryState.Pending, first.MonetaryState);
            Assert.False(first.RequiresReconciliation);

            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(first.OperationId, finalized.OperationId);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.NotEmpty(harness.RefundValues.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
        }

        [Fact]
        public async Task I_a_refund_that_stays_unresolved_is_read_back_and_never_paid_again()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Unknown;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var plan = (await harness.ExchangePlans.FindAsync(replay.OperationId))!;

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.False(replay.RequiresReconciliation);
            Assert.Equal(ExchangeMonetaryState.Pending, replay.MonetaryState);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.NotEmpty(harness.RefundValues.ObservedRecoveryKeys);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.NotNull(replay.SuccessorElectronicTicketId);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- J. a confirmed refund across a restart

        [Fact]
        public async Task J_a_refund_confirmed_in_the_provider_survives_a_process_restart_and_pays_once()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-refund-{Guid.NewGuid():N}");
            var provider = new DeterministicRefundValueAdapter { ThrowAfterDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, refundValues: provider);
            var scenario = await RefundAsync(setup, crashed, [1]);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Single(provider.ObservedRequests);

            provider.ThrowAfterDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, refundValues: provider);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(provider.ObservedRequests);
            Assert.Single(provider.ObservedRecoveryKeys);
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
            Assert.Equal(scenario.CustomerTotal - Owed, after.CustomerTotal);
        }

        // ---------------------------------------------------------------- K, L, M, N. contradiction and refusal

        [Theory]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        [InlineData("missing-reference")]
        [InlineData("wrong-disposition")]
        public async Task K_L_M_a_refund_that_contradicts_the_obligation_needs_reconciliation(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);

            switch (shape)
            {
                case "wrong-amount":
                    harness.RefundValues.AmountOverride = Owed - 1m;
                    break;
                case "wrong-currency":
                    harness.RefundValues.CurrencyOverride = scenario.Accepted.SaleCurrencyId + 7;
                    break;
                case "missing-reference":
                    harness.RefundValues.OmitValueMovementReference = true;
                    break;
                default:
                    harness.RefundValues.DispositionOverride = "SomewhereElse";
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.NotNull(plan.Successor);
            Assert.False(string.IsNullOrWhiteSpace(plan.RefundDueDetail));
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(predecessor.Exchanges);
            Assert.NotNull(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(scenario.CustomerTotal - Owed, after.CustomerTotal);
        }

        [Fact]
        public async Task N_a_refused_refund_after_a_confirmed_reissue_needs_reconciliation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);
            var key = NewKey();

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Rejected;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Equal(ExchangeMonetaryState.Rejected, replay.MonetaryState);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.True(plan.IsRefundDueRejected);
            Assert.NotNull(plan.Successor);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(predecessor.Exchanges);
            Assert.NotNull(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- O, P. replay and lineage

        [Fact]
        public async Task O_a_completed_refund_due_replay_returns_no_value_twice()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var before = await ReloadAsync(_fixture, scenario.OrderId);

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.True(replay.IsReplay);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(3, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
        }

        [Fact]
        public async Task P_a_repeated_refund_due_reissue_refunds_against_the_current_predecessor()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await RefundAsync(setup, harness, [1]);

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var successorId = first.SuccessorElectronicTicketId!.Value;
            var successor = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reloaded = await ReloadAsync(_fixture, scenario.OrderId);
            IReadOnlyList<long> secondChanged = [successor.Coupons.First().CurrentOrderServiceId];

            ComposeSettlement(harness, reloaded, secondChanged, ChangeMonetaryOutcome.Refund, Owed, "EXC-QUOTE-2");

            await harness.Exchange.QuoteAsync(scenario.OrderId, secondChanged);

            var second = await harness.Exchange.ExchangeAsync(new ExchangeExecution(
                scenario.OrderId, secondChanged, "EXC-QUOTE-2", NewKey(), reloaded.CommercialVersion));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var settled = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reissued = (await FindTicketAsync(_fixture, second.SuccessorElectronicTicketId!.Value))!;
            var refunds = harness.RefundValues.ObservedRequests;

            Assert.Equal(ServicingOperationStatus.Completed, second.OperationStatus);
            Assert.Equal(successorId, reissued.PredecessorElectronicTicketId);
            Assert.Equal(scenario.TicketId, settled.PredecessorElectronicTicketId);
            Assert.Equal(2, refunds.Count);
            Assert.Equal(predecessor.DocumentNumber, refunds[0].DocumentNumber);
            Assert.Equal(settled.DocumentNumber, refunds[1].DocumentNumber);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(settled.Exchanges);
        }

        // ---------------------------------------------------------------- support

        private Task<ExchangeScenario> RefundAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness,
            int[] changedCouponNumbers,
            int[]? flownCouponNumbers = null,
            decimal settlementAmount = Owed,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
            => NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Refund, changedCouponNumbers, flownCouponNumbers,
                settlementAmount, shapeAccepted, createOrder);

        private static AcceptedRefundDue Owing(AcceptedExchange accepted, decimal amount)
            => new(amount, accepted.SaleCurrencyId, AcceptedRefundDue.OriginalFormOfPayment);

        private async Task AssertNothingExternalAsync(
            OrderSliceHarness harness,
            ExchangeScenario scenario,
            int acceptCalls)
        {
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(acceptCalls, harness.ExchangeQuotes.ObservedSelections.Count);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"exc-refund-{Guid.NewGuid():N}"));
    }
}
