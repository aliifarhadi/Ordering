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
    public sealed class ResidualExchangeFlowTests
    {
        private const decimal Owed = ExchangeSourceFactory.NegativeBalanceAmount;

        private readonly OrderingDatabaseFixture _fixture;

        public ResidualExchangeFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- S, T. happy paths

        [Fact]
        public async Task S_a_fully_unused_residual_reissue_creates_one_authoritative_instrument()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var residual = Assert.Single(harness.ExchangeResiduals.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ChangeMonetaryOutcome.Residual, outcome.MonetaryOutcome);
            Assert.Equal(Owed, outcome.MonetaryAmount);
            Assert.Equal(ExchangeMonetaryState.Settled, outcome.MonetaryState);
            Assert.Equal(ExchangeSourceFactory.ResidualDisposition, outcome.MonetaryDisposition);
            Assert.False(outcome.RequiresReconciliation);
            Assert.False(string.IsNullOrWhiteSpace(outcome.ResidualInstrumentReference));
            Assert.Equal(ResidualInstrumentKind.Mco, outcome.ResidualInstrument);

            Assert.Equal(Owed, residual.Amount);
            Assert.Equal(predecessor.DocumentNumber, residual.PredecessorDocumentNumber);
            Assert.Equal(successor.DocumentNumber, residual.SuccessorDocumentNumber);
            Assert.Equal(predecessor.TravelerId, residual.BeneficiaryTravellerId);
            Assert.True(plan.IsResidualSettled);
            Assert.False(string.IsNullOrWhiteSpace(plan.ResidualInstrumentReference));

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
            Assert.Equal(scenario.CustomerTotal - Owed, after.CustomerTotal);
        }

        [Fact]
        public async Task T_a_partially_used_residual_reissue_keeps_used_coupons_historical()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(
                setup, harness, [2], [1], createOrder: candidate => candidate.CreateOnwardBoundOrderAsync());

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, outcome.SuccessorElectronicTicketId!.Value))!;
            var used = predecessor.Coupons.Single(coupon => coupon.CouponNumber == 1);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(TicketCouponFinancialStatus.Used, used.FinancialStatus);
            Assert.Equal(2, successor.Coupons.Count);
            Assert.DoesNotContain(successor.Coupons, coupon => coupon.PredecessorTicketCouponId == used.Id);
            Assert.Equal(
                scenario.CouponServiceIds[2],
                Assert.Single(Assert.Single(harness.ReservationChanges.ObservedApplies).Items).ReplacedOrderServiceId);
            Assert.Equal([2, 3], Assert.Single(harness.DocumentExchanges.ObservedRequests).Coupons.Select(coupon => coupon.PredecessorCouponNumber).Order());
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
        }

        // ---------------------------------------------------------------- U, V, W. authority and rail isolation

        [Fact]
        public async Task U_the_provider_residual_amount_is_preserved_exactly_end_to_end()
        {
            const decimal unusual = 96_431m;

            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1], settlementAmount: unusual);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(unusual, scenario.Accepted.Residual!.Amount);
            Assert.Equal(unusual, plan.Residual!.Amount);
            Assert.Equal(unusual, Assert.Single(harness.ExchangeResiduals.ObservedRequests).Amount);
            Assert.Equal(unusual, outcome.MonetaryAmount);
            Assert.Equal(scenario.Accepted.SaleCurrencyId, outcome.MonetaryCurrencyId);
            Assert.Equal(scenario.CustomerTotal - unusual, after.CustomerTotal);
            Assert.NotEqual(PricingSource.OrderingDerived, plan.PricingSource);
        }

        [Fact]
        public async Task V_residual_never_touches_the_funding_or_cash_refund_rails()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.ExchangeFunding.ObservedReleases);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.RefundValues.ObservedRecoveryKeys);
        }

        [Fact]
        public async Task W_a_residual_exchange_creates_no_miscellaneous_document()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);

            var before = await harness.MiscDocumentRepository.ListByOrderAsync(scenario.OrderId);

            await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await harness.MiscDocumentRepository.ListByOrderAsync(scenario.OrderId);

            Assert.Empty(before);
            Assert.Empty(after);
            Assert.Empty(harness.MiscDocuments.Requests);
        }

        // ---------------------------------------------------------------- X, Y, Z. residual recovery

        [Fact]
        public async Task X_an_unresolved_residual_that_resolves_on_read_back_finalizes_once()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeResiduals.FulfillOutcome = ProviderOperationOutcome.Pending;
            harness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Pending;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ExchangeMonetaryState.Pending, first.MonetaryState);
            Assert.False(first.RequiresReconciliation);

            harness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(first.OperationId, finalized.OperationId);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.NotEmpty(harness.ExchangeResiduals.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(predecessor.Exchanges);
            Assert.False(string.IsNullOrWhiteSpace(finalized.ResidualInstrumentReference));
        }

        [Fact]
        public async Task Y_a_residual_that_stays_unresolved_is_read_back_and_never_issued_again()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeResiduals.FulfillOutcome = ProviderOperationOutcome.Unknown;
            harness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var plan = (await harness.ExchangePlans.FindAsync(replay.OperationId))!;

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.False(replay.RequiresReconciliation);
            Assert.Equal(ExchangeMonetaryState.Pending, replay.MonetaryState);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.NotEmpty(harness.ExchangeResiduals.ObservedRecoveryKeys);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.Null(replay.SuccessorElectronicTicketId);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task Z_a_residual_created_in_the_provider_survives_a_process_restart_without_a_duplicate()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-residual-{Guid.NewGuid():N}");
            var provider = new DeterministicExchangeResidualAdapter { ThrowAfterDispatch = true };

            await using var setup = NewHarness();
            await using var crashed = new OrderSliceHarness(_fixture, caller, exchangeResiduals: provider);
            var scenario = await ResidualAsync(setup, crashed, [1]);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var dispatched = Assert.Single(provider.ObservedRequests);

            provider.ThrowAfterDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, exchangeResiduals: provider);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var plan = (await resumed.ExchangePlans.FindAsync(finalized.OperationId))!;

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(provider.ObservedRequests);
            Assert.Equal(dispatched.OperationKey, Assert.Single(provider.ObservedRecoveryKeys));
            Assert.Empty(resumed.DocumentExchanges.ObservedRequests);
            Assert.Equal($"INSTR-{plan.Successor!.DocumentNumber}", plan.ResidualInstrumentReference);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Single(await TicketsAsync(_fixture, scenario.OrderId), candidate => candidate.PredecessorElectronicTicketId == predecessor.Id);
        }

        // ---------------------------------------------------------------- AA, AB, AC, AD. contradiction and refusal

        [Theory]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        [InlineData("no-instrument")]
        [InlineData("no-provider-reference")]
        public async Task AA_AB_AC_a_residual_that_contradicts_the_obligation_needs_reconciliation(string shape)
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);

            switch (shape)
            {
                case "wrong-amount":
                    harness.ExchangeResiduals.AmountOverride = Owed + 3m;
                    break;
                case "wrong-currency":
                    harness.ExchangeResiduals.CurrencyOverride = scenario.Accepted.SaleCurrencyId + 7;
                    break;
                case "no-instrument":
                    harness.ExchangeResiduals.OmitInstrumentReference = true;
                    break;
                default:
                    harness.ExchangeResiduals.OmitProviderReference = true;
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
            Assert.False(string.IsNullOrWhiteSpace(plan.ResidualDetail));
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(predecessor.Exchanges);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(scenario.CustomerTotal, after.CustomerTotal);
        }

        [Fact]
        public async Task AD_a_refused_residual_after_a_confirmed_reissue_needs_reconciliation()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);
            var key = NewKey();

            harness.ExchangeResiduals.FulfillOutcome = ProviderOperationOutcome.Rejected;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var plan = (await harness.ExchangePlans.FindAsync(first.OperationId))!;
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Equal(ExchangeMonetaryState.Rejected, replay.MonetaryState);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.True(plan.IsResidualRejected);
            Assert.NotNull(plan.Successor);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ElectronicTicketStatus.Issued, predecessor.StatusSummary);
            Assert.Null(await FindTicketAsync(_fixture, plan.SuccessorElectronicTicketId));
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- AF, AG, AH. replay, lineage, observability

        [Fact]
        public async Task AF_a_completed_residual_replay_creates_no_second_instrument()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var before = await ReloadAsync(_fixture, scenario.OrderId);

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.True(replay.IsReplay);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Single(harness.ExchangeQuotes.ObservedSelections);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(3, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
        }

        [Fact]
        public async Task AG_a_repeated_residual_reissue_uses_the_current_accountable_predecessor()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await ResidualAsync(setup, harness, [1]);

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var successorId = first.SuccessorElectronicTicketId!.Value;
            var successor = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reloaded = await ReloadAsync(_fixture, scenario.OrderId);
            IReadOnlyList<long> secondChanged = [successor.Coupons.First().CurrentOrderServiceId];

            ComposeSettlement(harness, reloaded, secondChanged, ChangeMonetaryOutcome.Residual, Owed, "EXC-QUOTE-2");

            await harness.Exchange.QuoteAsync(scenario.OrderId, secondChanged);

            var second = await harness.Exchange.ExchangeAsync(new ExchangeExecution(
                scenario.OrderId, secondChanged, "EXC-QUOTE-2", NewKey(), reloaded.CommercialVersion));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var settled = await TicketAsync(_fixture, scenario.OrderId, successorId);
            var reissued = (await FindTicketAsync(_fixture, second.SuccessorElectronicTicketId!.Value))!;
            var residuals = harness.ExchangeResiduals.ObservedRequests;

            Assert.Equal(ServicingOperationStatus.Completed, second.OperationStatus);
            Assert.Equal(successorId, reissued.PredecessorElectronicTicketId);
            Assert.Equal(scenario.TicketId, settled.PredecessorElectronicTicketId);
            Assert.Equal(2, residuals.Count);
            Assert.Equal(predecessor.DocumentNumber, residuals[0].PredecessorDocumentNumber);
            Assert.Equal(settled.DocumentNumber, residuals[1].PredecessorDocumentNumber);
            Assert.NotEqual(residuals[0].OperationKey, residuals[1].OperationKey);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(settled.Exchanges);
        }

        [Fact]
        public async Task AH_a_consumer_can_tell_residual_state_apart_from_cash_refund_state()
        {
            await using var residualSetup = NewHarness();
            await using var residualHarness = NewHarness();
            var residualScenario = await ResidualAsync(residualSetup, residualHarness, [1]);

            residualHarness.ExchangeResiduals.FulfillOutcome = ProviderOperationOutcome.Pending;
            residualHarness.ExchangeResiduals.RecoveryOutcome = ProviderOperationOutcome.Pending;

            var residualPending = await residualHarness.Exchange.ExchangeAsync(residualScenario.Execution(NewKey()));

            await using var refundSetup = NewHarness();
            await using var refundHarness = NewHarness();
            var refundScenario = await NegativeBalanceAsync(
                _fixture, refundSetup, refundHarness, ChangeMonetaryOutcome.Refund, [1]);

            refundHarness.RefundValues.RequestOutcome = ProviderOperationOutcome.Pending;
            refundHarness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Pending;

            var refundPending = await refundHarness.Exchange.ExchangeAsync(refundScenario.Execution(NewKey()));

            Assert.Equal(ChangeMonetaryOutcome.Residual, residualPending.MonetaryOutcome);
            Assert.Equal(ChangeMonetaryOutcome.Refund, refundPending.MonetaryOutcome);
            Assert.Equal(ExchangeMonetaryState.Pending, residualPending.MonetaryState);
            Assert.Equal(ExchangeMonetaryState.Pending, refundPending.MonetaryState);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, residualPending.DocumentOutcome);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, refundPending.DocumentOutcome);
            Assert.NotEqual(residualPending.MonetaryDisposition, refundPending.MonetaryDisposition);
            Assert.Equal(AcceptedRefundDue.OriginalFormOfPayment, refundPending.MonetaryDisposition);
            Assert.Null(refundPending.ResidualInstrumentReference);
            Assert.Null(refundPending.ResidualInstrument);
            Assert.False(string.IsNullOrWhiteSpace(residualPending.ResidualInstrumentReference));
            Assert.Equal(ResidualInstrumentKind.Mco, residualPending.ResidualInstrument);
            Assert.False(residualPending.RequiresReconciliation);
            Assert.False(refundPending.RequiresReconciliation);
        }

        // ---------------------------------------------------------------- support

        private Task<ExchangeScenario> ResidualAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness,
            int[] changedCouponNumbers,
            int[]? flownCouponNumbers = null,
            decimal settlementAmount = Owed,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
            => NegativeBalanceAsync(
                _fixture, setup, harness, ChangeMonetaryOutcome.Residual, changedCouponNumbers, flownCouponNumbers,
                settlementAmount, shapeAccepted, createOrder);

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"exc-residual-{Guid.NewGuid():N}"));
    }
}
