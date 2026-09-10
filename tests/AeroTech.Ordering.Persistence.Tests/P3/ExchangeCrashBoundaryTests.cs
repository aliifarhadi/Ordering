using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ExchangeCrashBoundaryTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public ExchangeCrashBoundaryTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------------------------- C7/C8/N1. AirPrice acceptance

        [Fact]
        public async Task C7_N1_an_accept_failure_retains_the_claim_and_persists_no_plan()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            harness.ExchangeQuotes.ThrowOnAccept = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            Assert.Empty(harness.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Null(await harness.ExchangePlans.FindAsync(harness.ExchangeQuotes.ObservedSelections.Single().OperationId));
        }

        [Fact]
        public async Task C8_an_accept_replay_reuses_the_same_operation_key_and_caller_intent()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ExchangeQuotes.ThrowOnAccept = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            harness.ExchangeQuotes.ThrowOnAccept = false;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var selections = harness.ExchangeQuotes.ObservedSelections;

            Assert.Equal(2, selections.Count);
            Assert.Equal(selections[0].OperationKey, selections[1].OperationKey);
            Assert.Equal(selections[0].ChangedOrderServiceIds, selections[1].ChangedOrderServiceIds);
            Assert.Equal($"exchange-quote:{outcome.OperationId}", selections[1].OperationKey);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
        }

        // ---------------------------------------------------------------- F. inventory

        [Fact]
        public async Task F1_a_fresh_confirmed_reservation_applies_once_without_a_read_back()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task F2_F3_N2_an_unresolved_reservation_makes_no_document_call_and_holds_the_claim(ProviderOperationOutcome applyOutcome)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            harness.ReservationChanges.ApplyOutcome = applyOutcome;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Equal(applyOutcome, plan!.ReservationOutcome);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            Assert.Equal(scenario.CommercialVersion, (await ReloadAsync(_fixture, scenario.OrderId)).CommercialVersion);
        }

        [Fact]
        public async Task F4_a_rejected_reservation_ends_terminally_without_document_or_local_mutation()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Equal(ExchangeDocumentOutcome.NotAttempted, outcome.DocumentOutcome);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            await AssertNoLocalExchangeAsync(scenario);

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.Rejected, replay.OperationStatus);
            Assert.True(replay.IsReplay);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task F5_F7_a_failure_before_dispatch_retains_the_claim_then_applies_exactly_once()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ReservationChanges.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            Assert.Empty(harness.ReservationChanges.DispatchedKeys);

            harness.ReservationChanges.ThrowBeforeDispatch = false;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Single(harness.ReservationChanges.DispatchedKeys);
            Assert.Equal(harness.ReservationChanges.ObservedApplies[^1].OperationKey, harness.ReservationChanges.ObservedRecoveryKeys.Single());
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, recovered.DocumentOutcome);
        }

        [Fact]
        public async Task F6_F8_a_failure_after_dispatch_retains_the_claim_and_recovers_without_a_second_apply()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ReservationChanges.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.ReservationChanges.ThrowAfterDispatch = false;
            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, recovered.DocumentOutcome);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task F9_a_dispatched_reservation_still_unresolved_never_applies_again(ProviderOperationOutcome recoveryOutcome)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;
            harness.ReservationChanges.RecoveryOutcome = recoveryOutcome;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task F10_a_dispatched_reservation_recovered_rejected_never_applies_again_and_ends_terminally()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Rejected;

            var rejected = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.DispatchedKeys);
            Assert.Equal(ServicingOperationStatus.Rejected, rejected.OperationStatus);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            await AssertNoLocalExchangeAsync(scenario);
        }

        // ---------------------------------------------------------------- G. inventory -> document durable boundary

        [Fact]
        public async Task G1_G2_reservation_confirmation_is_durable_before_the_first_document_call_and_replay_recovers_first()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.DocumentExchanges.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            var plan = await PlanAsync(harness);

            Assert.True(plan.IsReservationConfirmed);
            Assert.NotNull(plan.ReservationExternalRef);
            Assert.Null(plan.DocumentExchangeOutcome);
            Assert.Null(plan.Successor);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.DispatchedKeys);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.DocumentExchanges.ThrowBeforeDispatch = false;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(2, harness.DocumentExchanges.ObservedRequests.Count);
            Assert.Equal(harness.DocumentExchanges.ObservedRequests[^1].OperationKey, harness.DocumentExchanges.ObservedRecoveryKeys.Single());
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, recovered.DocumentOutcome);
        }

        [Fact]
        public async Task G3_a_recovered_reservation_confirmation_enters_the_document_stage_recover_first()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, recovered.DocumentOutcome);
        }

        // ---------------------------------------------------------------- H. document exchange

        [Fact]
        public async Task H1_a_fresh_confirmed_document_exchange_finalizes_once()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = await harness.ExchangePlans.FindAsync(outcome.OperationId);

            Assert.True(plan!.IsDocumentExchangeConfirmed);
            Assert.Equal(outcome.SuccessorDocumentNumber, plan.Successor!.DocumentNumber);
            Assert.Equal(outcome.ProviderExchangeReference, plan.DocumentExchangeProviderReference);
            Assert.Equal(1, Assert.Single(plan.Successor.Coupons).CouponNumber);
            Assert.Equal(1, Assert.Single(plan.Coupons).SuccessorCouponNumber);
            Assert.Single(harness.DocumentExchanges.DispatchedKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending, ServicingOperationStatus.AwaitingExternal, ExchangeDocumentOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown, ServicingOperationStatus.AwaitingExternal, ExchangeDocumentOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Rejected, ServicingOperationStatus.NeedsReconciliation, ExchangeDocumentOutcome.Rejected)]
        public async Task H2_H4_M1_M3_a_nonconfirmed_document_outcome_is_durable_holds_the_claim_and_never_finalizes(
            ProviderOperationOutcome outcome,
            ServicingOperationStatus expectedStatus,
            ExchangeDocumentOutcome expectedDocumentOutcome)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            harness.DocumentExchanges.ExchangeOutcome = outcome;

            var result = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = await PlanAsync(harness);

            Assert.Equal(expectedStatus, result.OperationStatus);
            Assert.Equal(expectedDocumentOutcome, result.DocumentOutcome);
            Assert.Equal(outcome, plan.DocumentExchangeOutcome);
            Assert.True(plan.IsReservationConfirmed);
            Assert.False(plan.IsDocumentExchangeConfirmed);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            await AssertNoLocalExchangeAsync(scenario);
        }

        [Fact]
        public async Task H5_H7_a_document_failure_before_dispatch_recovers_then_exchanges_once()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.DocumentExchanges.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.DocumentExchanges.ThrowBeforeDispatch = false;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.DispatchedKeys);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, recovered.DocumentOutcome);
        }

        [Fact]
        public async Task H6_H8_H11_a_document_failure_after_dispatch_never_redispatches_and_recovers_under_the_same_key()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.DocumentExchanges.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));

            harness.DocumentExchanges.ThrowAfterDispatch = false;
            harness.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(harness.DocumentExchanges.ObservedRequests.Single().OperationKey, harness.DocumentExchanges.ObservedRecoveryKeys.Single());
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, recovered.DocumentOutcome);
            Assert.Equal($"EXC{recovered.OperationId}", recovered.SuccessorDocumentNumber);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending, ServicingOperationStatus.AwaitingExternal)]
        [InlineData(ProviderOperationOutcome.Unknown, ServicingOperationStatus.AwaitingExternal)]
        [InlineData(ProviderOperationOutcome.Rejected, ServicingOperationStatus.NeedsReconciliation)]
        public async Task H9_H10_M2_M4_a_dispatched_document_exchange_is_never_exchanged_again(
            ProviderOperationOutcome recoveryOutcome,
            ServicingOperationStatus expected)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Unknown;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            harness.DocumentExchanges.RecoveryOutcome = recoveryOutcome;

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var again = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.NotEmpty(harness.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(expected, replay.OperationStatus);
            Assert.Equal(expected, again.OperationStatus);
            Assert.NotEqual(ExchangeDocumentOutcome.Exchanged, replay.DocumentOutcome);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            await AssertNoLocalExchangeAsync(scenario);
        }

        [Fact]
        public async Task H12_a_conflicting_recovered_successor_identity_needs_reconciliation()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            harness.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Pending;
            harness.DocumentExchanges.ReportSuccessorWhilePending = true;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var pendingPlan = await PlanAsync(harness);

            Assert.NotNull(pendingPlan.Successor);

            harness.DocumentExchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            harness.DocumentExchanges.RecoveredSuccessorDocumentNumber = "EXC-CONFLICT";

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var plan = await PlanAsync(harness);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Equal(pendingPlan.Successor!.DocumentNumber, plan.Successor!.DocumentNumber);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            await AssertNoLocalExchangeAsync(scenario);
        }

        [Theory]
        [InlineData("predecessor")]
        [InlineData("unrelated")]
        public async Task H12_S33_an_unusable_successor_number_needs_reconciliation_without_a_second_local_ticket(string collision)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var tickets = await TicketsAsync(_fixture, scenario.OrderId);

            harness.DocumentExchanges.SuccessorDocumentNumber = collision == "predecessor"
                ? tickets.Single(ticket => ticket.Id == scenario.TicketId).DocumentNumber
                : tickets.Single(ticket => ticket.Id != scenario.TicketId).DocumentNumber;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Equal(2, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            await AssertNoLocalExchangeAsync(scenario);
        }

        [Fact]
        public async Task A_host_result_without_a_coupon_identity_for_every_predecessor_coupon_needs_reconciliation()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            harness.DocumentExchanges.OmitSuccessorCoupons = true;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var plan = await PlanAsync(harness);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(plan.IsDocumentExchangeConfirmed);
            Assert.Empty(plan.Successor!.Coupons);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
            await AssertNoLocalExchangeAsync(scenario);
        }

        // ---------------------------------------------------------------- I. document confirmed -> local commit

        [Fact]
        public async Task I1_I2_durable_document_confirmation_finalizes_on_replay_with_zero_external_calls()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-crash-{Guid.NewGuid():N}");

            await using var setup = NewHarness(caller);
            var scenario = await TicketedAsync(_fixture, setup);
            var key = NewKey();

            setup.DocumentExchanges.ExchangeOutcome = ProviderOperationOutcome.Unknown;

            var first = await setup.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);

            await setup.ExchangePlans.RecordDocumentExchangeOutcomeAsync(
                first.OperationId,
                ProviderOperationOutcome.Confirmed,
                "EXCH-RECOVERED",
                new SuccessorDocumentIdentity($"EXC{first.OperationId}", 1, null, DocumentAuthority.Local, null, [new SuccessorCouponIdentity(scenario.CouponId, 1)]),
                null);
            await setup.UnitOfWork.SaveChangesAsync();

            await AssertNoLocalExchangeAsync(scenario);

            await using var resume = NewHarness(caller);
            Register(resume, scenario);

            var finalized = await resume.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(first.OperationId, finalized.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal("EXCH-RECOVERED", finalized.ProviderExchangeReference);
            Assert.Empty(resume.ExchangeQuotes.ObservedSelections);
            Assert.Empty(resume.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(resume.ReservationChanges.ObservedApplies);
            Assert.Empty(resume.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(resume.DocumentExchanges.ObservedRequests);
            Assert.Empty(resume.DocumentExchanges.ObservedRecoveryKeys);

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(predecessor.Exchanges);
            Assert.Equal(scenario.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(scenario.DocumentVersion + 1, predecessor.DocumentVersion);
            Assert.Equal(3, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(resume, scenario));
        }

        // ---------------------------------------------------------------- O. resume/version protection

        [Fact]
        public async Task O1_a_commercial_version_mismatch_on_resume_fails_closed_with_zero_external_calls()
        {
            var caller = TestCallerContexts.AirlineUser(7401, $"exc-crash-{Guid.NewGuid():N}");

            await using var setup = NewHarness(caller);
            var scenario = await TicketedAsync(_fixture, setup);
            var key = NewKey();

            setup.DocumentExchanges.EligibilityOutcome = DocumentExchangeEligibilityOutcome.PendingEvidence;

            var first = await setup.Exchange.ExchangeAsync(scenario.Execution(key));

            await BumpCommercialVersionAsync(scenario.OrderId);

            await using var resume = NewHarness(caller);
            Register(resume, scenario);

            var resumed = await resume.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, resumed.OperationStatus);
            Assert.Empty(resume.ExchangeQuotes.ObservedSelections);
            Assert.Empty(resume.DocumentExchanges.ObservedEligibilityRequests);
            Assert.Empty(resume.ReservationChanges.ObservedApplies);
            Assert.Empty(resume.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(resume.DocumentExchanges.ObservedRequests);
            Assert.Empty(resume.DocumentExchanges.ObservedRecoveryKeys);
            Assert.Equal(scenario.CommercialVersion, (await resume.ExchangePlans.FindAsync(first.OperationId))!.ExpectedCommercialVersion);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(resume, scenario));
            await AssertNoLocalExchangeAsync(scenario, commercialVersion: scenario.CommercialVersion + 1);
        }

        // ---------------------------------------------------------------- P. completed replay

        [Fact]
        public async Task P1_P9_N7_a_completed_replay_makes_no_external_call_creates_nothing_and_releases_its_claim()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var settled = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessorSettled = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var ticketsSettled = (await TicketsAsync(_fixture, scenario.OrderId)).Count;
            var accepts = harness.ExchangeQuotes.ObservedSelections.Count;
            var eligibility = harness.DocumentExchanges.ObservedEligibilityRequests.Count;
            var applies = harness.ReservationChanges.ObservedApplies.Count;
            var exchanges = harness.DocumentExchanges.ObservedRequests.Count;

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = (await FindTicketAsync(_fixture, first.SuccessorElectronicTicketId!.Value))!;

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Equal(first.Coupons, replay.Coupons);
            Assert.Equal(first.PriceChangeSetId, replay.PriceChangeSetId);
            Assert.Equal(first.SuccessorDocumentNumber, replay.SuccessorDocumentNumber);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);

            Assert.Equal(accepts, harness.ExchangeQuotes.ObservedSelections.Count);
            Assert.Equal(eligibility, harness.DocumentExchanges.ObservedEligibilityRequests.Count);
            Assert.Equal(applies, harness.ReservationChanges.ObservedApplies.Count);
            Assert.Equal(exchanges, harness.DocumentExchanges.ObservedRequests.Count);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(harness.DocumentExchanges.ObservedRecoveryKeys);

            Assert.Equal(ticketsSettled, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
            Assert.Single(successor.Coupons);
            Assert.Single(predecessor.Exchanges);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Exchange);
            Assert.Equal(settled.PricingLines.Count, after.PricingLines.Count);
            Assert.Equal(settled.FinancialSequence, after.FinancialSequence);
            Assert.Equal(settled.CommercialVersion, after.CommercialVersion);
            Assert.Equal(predecessorSettled.DocumentVersion, predecessor.DocumentVersion);
            Assert.Equal(settled.OrderServices.Count, after.OrderServices.Count);

            Assert.NotEqual(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        // ---------------------------------------------------------------- support

        private async Task AssertNoLocalExchangeAsync(ExchangeScenario scenario, int? commercialVersion = null)
        {
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            Assert.Equal(commercialVersion ?? scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.FinancialSequence, after.FinancialSequence);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Empty(predecessor.Exchanges);
            Assert.Equal(ElectronicTicketStatus.Issued, predecessor.StatusSummary);
            Assert.Equal(scenario.DocumentVersion, predecessor.DocumentVersion);
            Assert.Equal(2, (await TicketsAsync(_fixture, scenario.OrderId)).Count);
        }

        private async Task<Domain.Servicing.Plans.AcceptedExchangePlan> PlanAsync(OrderSliceHarness harness)
        {
            var operationId = harness.ExchangeQuotes.ObservedSelections[^1].OperationId;
            var plan = await harness.ExchangePlans.FindAsync(operationId);

            Assert.NotNull(plan);

            return plan!;
        }

        private async Task BumpCommercialVersionAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[Orders] SET [CommercialVersion] = [CommercialVersion] + 1 WHERE [Id] = {0}",
                orderId);
        }

        private OrderSliceHarness NewHarness(Domain._Shared.Contracts.ICallerContext? caller = null)
            => new(_fixture, caller ?? TestCallerContexts.AirlineUser(7401, $"exc-crash-{Guid.NewGuid():N}"));
    }
}
