using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
using AeroTech.Ordering.Providers.Unconfigured;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class EmdReassociationFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document = NewDocumentNumber();
        private readonly string _secondDocument = NewDocumentNumber();

        public EmdReassociationFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ------------------------------------------- G1-C1..C5. ticket truth is independent of ancillary settlement

        [Fact]
        public async Task G1_C1_a_confirmed_move_completes_the_exchange_and_walks_the_whole_association_lifecycle()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var predecessorBefore = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var predecessorCouponNumber = predecessorBefore.Coupons
                .Single(coupon => coupon.Id == scenario.CouponId).CouponNumber;

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = await SuccessorAsync(scenario, outcome);
            var successorCoupon = successor.Coupons.Single();
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();
            var history = coupon.AssociationChanges;

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.All(
                predecessor.Coupons,
                candidate => Assert.Equal(TicketCouponFinancialStatus.Exchanged, candidate.FinancialStatus));
            Assert.Single(predecessor.Exchanges);
            Assert.Equal(predecessor.Id, successor.PredecessorElectronicTicketId);

            Assert.Equal(
                [
                    EmdCouponAssociationChangeKind.Associated,
                    EmdCouponAssociationChangeKind.DisassociatedByReissue,
                    EmdCouponAssociationChangeKind.Reassociated
                ],
                history.Select(change => change.Kind).ToList());
            Assert.Equal([1, 2, 3], history.Select(change => change.Sequence).ToList());
            Assert.Equal(successorCoupon.Id, coupon.AssociatedTicketCouponId);

            var disassociated = history[1];
            var reassociated = history[2];

            Assert.Equal(scenario.CouponId, disassociated.PreviousTicketCouponId);
            Assert.Equal(predecessorBefore.DocumentNumber, disassociated.PreviousDocumentNumber);
            Assert.Equal(predecessorCouponNumber, disassociated.PreviousCouponNumber);
            Assert.Null(disassociated.CurrentTicketCouponId);
            Assert.Equal(outcome.OperationId, disassociated.OperationId);
            Assert.Equal(outcome.ProviderExchangeReference, disassociated.ProviderReference);

            Assert.Equal(scenario.CouponId, reassociated.PreviousTicketCouponId);
            Assert.Equal(successorCoupon.Id, reassociated.CurrentTicketCouponId);
            Assert.Equal(successor.DocumentNumber, reassociated.CurrentDocumentNumber);
            Assert.Equal(Assert.Single(outcome.Ancillaries).ProviderReference, reassociated.ProviderReference);

            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task G1_C2_C3_an_unresolved_move_leaves_the_reissue_authoritative_and_the_ancillary_detached(
            ProviderOperationOutcome unresolved)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.ReassociateOutcome = unresolved;
            harness.EmdAssociations.RecoveryOutcome = unresolved;

            var held = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = await SuccessorAsync(scenario, held);
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.False(replay.RequiresReconciliation);
            Assert.Equal(ExchangeAncillaryState.Pending, replay.AncillaryState);

            Assert.NotNull(held.SuccessorElectronicTicketId);
            Assert.Equal(held.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, replay.DocumentOutcome);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(predecessor.Exchanges);
            Assert.Equal(predecessor.Id, successor.PredecessorElectronicTicketId);

            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);

            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.NotEmpty(harness.EmdAssociations.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task G1_C4_a_refused_move_reconciles_without_undoing_the_reissue()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.ReassociateOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var successor = await SuccessorAsync(scenario, outcome);
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();
            var plan = (await harness.ExchangePlans.FindAsync(outcome.OperationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.Equal(ExchangeAncillaryState.Rejected, outcome.AncillaryState);

            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Equal(predecessor.Id, successor.PredecessorElectronicTicketId);
            Assert.Single(
                (await ReloadAsync(_fixture, scenario.OrderId)).Changes,
                change => change.ChangeType == OrderChangeType.Exchange);

            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
            Assert.True(plan.HasRejectedAncillary);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
        }

        [Theory]
        [InlineData("coupon")]
        [InlineData("document")]
        [InlineData("reference")]
        public async Task G1_C5_a_contradictory_confirmation_reconciles_without_undoing_the_reissue(string contradiction)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            switch (contradiction)
            {
                case "coupon":
                    harness.EmdAssociations.ReportedAssociatedCouponNumber = 9;
                    break;
                case "document":
                    harness.EmdAssociations.ReportedEmdDocumentNumber = NewDocumentNumber();
                    break;
                default:
                    harness.EmdAssociations.OmitProviderReference = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
            Assert.False(string.IsNullOrWhiteSpace(Assert.Single(outcome.Ancillaries).Detail));
        }

        // ------------------------------------------- G1-C6..C10. crash boundaries

        [Fact]
        public async Task G1_C6_a_document_confirmation_the_caller_never_saw_still_materializes_one_successor()
        {
            var caller = Caller();
            var documents = new DeterministicDocumentExchangeAdapter { ThrowAfterDispatch = true };

            await using var crashed = new OrderSliceHarness(_fixture, caller, documentExchanges: documents);
            var scenario = await TicketedAsync(_fixture, crashed);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, crashed, scenario.OrderId, _document, [scenario.CouponId]);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var dispatched = Assert.Single(documents.ObservedRequests);

            documents.ThrowAfterDispatch = false;
            documents.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(_fixture, caller, documentExchanges: documents);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(documents.ObservedRequests);
            Assert.Equal(dispatched.OperationKey, Assert.Single(documents.ObservedRecoveryKeys));
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
        }

        [Fact]
        public async Task G1_C7_C8_a_materialized_reissue_never_disassociates_the_ancillary_twice()
        {
            var caller = Caller();
            var associations = new DeterministicEmdAssociationAdapter { ThrowBeforeDispatch = true };

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            var scenario = await TicketedAsync(_fixture, crashed);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, crashed, scenario.OrderId, _document, [scenario.CouponId]);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var afterCrash = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Null(afterCrash.Coupons.Single().AssociatedTicketCouponId);
            Assert.Single(
                afterCrash.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Empty(associations.DispatchedKeys);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);

            associations.ThrowBeforeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var document = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var coupon = document.Coupons.Single();

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
            Assert.Equal(afterCrash.DocumentVersion + 1, document.DocumentVersion);
            Assert.Equal(2, associations.ObservedRequests.Count);
            Assert.Single(associations.DispatchedKeys);
        }

        [Fact]
        public async Task G1_C9_C10_a_move_the_caller_never_saw_is_read_back_and_applied_once()
        {
            var caller = Caller();
            var associations = new DeterministicEmdAssociationAdapter { ThrowAfterDispatch = true };

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            var scenario = await TicketedAsync(_fixture, crashed);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, crashed, scenario.OrderId, _document, [scenario.CouponId]);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var dispatched = Assert.Single(associations.ObservedRequests);

            associations.ThrowAfterDispatch = false;
            associations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();
            var successor = await SuccessorAsync(scenario, finalized);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(associations.ObservedRequests);
            Assert.Equal(dispatched.OperationKey, Assert.Single(associations.ObservedRecoveryKeys));
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
            Assert.Equal(successor.Coupons.Single().Id, coupon.AssociatedTicketCouponId);
        }

        // ------------------------------------------- G1-C11..C12. the decision is bound to its own context

        [Fact]
        public async Task G1_C11_a_decision_naming_another_quoted_exchange_is_refused_before_irreversible_work()
        {
            var refusal = await RefusedAsync(harness =>
                harness.AncillaryDispositions.QuotedExchangeIdOverride = "EXC-SOMETHING-ELSE");

            Assert.Equal(20304, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
        }

        [Fact]
        public async Task G1_C12_a_decision_for_an_identical_looking_but_different_context_is_refused()
        {
            var refusal = await RefusedAsync(harness =>
                harness.AncillaryDispositions.ContextFingerprintOverride =
                    "0000000000000000000000000000000000000000000000000000000000000000");

            Assert.Equal(20304, refusal.Code);
        }

        // ------------------------------------------- retained G1 coverage

        [Fact]
        public async Task The_move_is_requested_in_accountable_document_terms_only()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var request = Assert.Single(harness.EmdAssociations.ObservedRequests);

            Assert.Equal(scenario.OrderId, request.OrderId);
            Assert.Equal(outcome.OperationId, request.OperationId);
            Assert.Equal(_document, request.EmdDocumentNumber);
            Assert.Equal(1, request.EmdCouponNumber);
            Assert.Equal(predecessor.DocumentNumber, request.PredecessorDocumentNumber);
            Assert.Equal(
                predecessor.Coupons.Single(coupon => coupon.Id == scenario.CouponId).CouponNumber,
                request.PredecessorCouponNumber);
            Assert.Equal(outcome.SuccessorDocumentNumber, request.SuccessorDocumentNumber);
            Assert.Equal(predecessor.TravelerId, request.BeneficiaryTravellerId);
            Assert.Equal(OrderSliceHarness.HomeAirlineId, request.IssuerCarrierId);
            Assert.Contains($"{_document}:1", request.OperationKey);
            Assert.Contains(outcome.OperationId.ToString(), request.OperationKey);
        }

        [Fact]
        public async Task The_move_happens_only_after_the_money_has_settled()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _document, [ticket.Coupons.First().Id]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeFundingState.Captured, outcome.FundingState);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
        }

        [Fact]
        public async Task An_unresolved_move_that_resolves_on_read_back_finalizes_once()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.ReassociateOutcome = ProviderOperationOutcome.Pending;
            harness.EmdAssociations.RecoveryOutcome = ProviderOperationOutcome.Pending;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            harness.EmdAssociations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(first.OperationId, finalized.OperationId);
            Assert.Equal(first.SuccessorElectronicTicketId, finalized.SuccessorElectronicTicketId);
            Assert.Equal(ExchangeAncillaryState.Confirmed, finalized.AncillaryState);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Equal(
                (await SuccessorAsync(scenario, finalized)).Coupons.Single().Id,
                coupon.AssociatedTicketCouponId);
        }

        [Fact]
        public async Task Every_affected_ancillary_moves_under_its_own_stable_key()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var successorCouponId = (await SuccessorAsync(scenario, outcome)).Coupons.Single().Id;

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(2, outcome.Ancillaries.Count);
            Assert.Equal(2, harness.EmdAssociations.ObservedRequests.Count);
            Assert.Equal(2, harness.EmdAssociations.DispatchedKeys.Distinct().Count());
            Assert.Equal(
                outcome.Ancillaries.Select(ancillary => ancillary.LegIdentity).Distinct().Count(),
                outcome.Ancillaries.Count);
            Assert.All(
                await AncillariesAsync(_fixture, scenario.OrderId),
                document => Assert.Equal(successorCouponId, document.Coupons.Single().AssociatedTicketCouponId));
        }

        [Fact]
        public async Task The_affected_ancillaries_are_settled_in_a_deterministic_order()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var ordered = new[] { _document, _secondDocument }.Order(StringComparer.Ordinal).ToList();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(
                ordered,
                harness.EmdAssociations.ObservedRequests.Select(request => request.EmdDocumentNumber).ToList());
            Assert.Equal(ordered, outcome.Ancillaries.Select(ancillary => ancillary.EmdDocumentNumber).ToList());
        }

        [Fact]
        public async Task One_unresolved_ancillary_holds_completion_without_repeating_the_settled_one()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();
            var last = new[] { _document, _secondDocument }.Order(StringComparer.Ordinal).Last();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            harness.EmdAssociations.OutcomeByCoupon[$"{last}:1"] = ProviderOperationOutcome.Pending;
            harness.EmdAssociations.RecoveryOutcome = ProviderOperationOutcome.Pending;

            var held = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Pending, held.AncillaryState);
            Assert.NotNull(held.SuccessorElectronicTicketId);
            Assert.Equal(2, harness.EmdAssociations.ObservedRequests.Count);

            harness.EmdAssociations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var successorCouponId = (await SuccessorAsync(scenario, finalized)).Coupons.Single().Id;

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(held.SuccessorElectronicTicketId, finalized.SuccessorElectronicTicketId);
            Assert.Equal(2, harness.EmdAssociations.ObservedRequests.Count);
            Assert.All(
                await AncillariesAsync(_fixture, scenario.OrderId),
                document => Assert.Equal(successorCouponId, document.Coupons.Single().AssociatedTicketCouponId));
            Assert.All(
                (await AncillariesAsync(_fixture, scenario.OrderId)).SelectMany(document => document.Coupons),
                coupon => Assert.Single(
                    coupon.AssociationChanges,
                    change => change.Kind == EmdCouponAssociationChangeKind.Reassociated));
        }

        [Fact]
        public async Task The_moved_ancillary_document_is_versioned_once_per_transition()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var attached = await AttachAncillaryAsync(
                _fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            var before = attached.DocumentVersion;

            await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(before + 2, (await AncillaryAsync(_fixture, scenario.OrderId, _document)).DocumentVersion);
        }

        [Fact]
        public async Task The_accepted_ancillary_plan_survives_a_process_restart()
        {
            var caller = Caller();
            var associations = new DeterministicEmdAssociationAdapter
            {
                ReassociateOutcome = ProviderOperationOutcome.Pending,
                RecoveryOutcome = ProviderOperationOutcome.Pending
            };

            await using var held = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            var scenario = await TicketedAsync(_fixture, held);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, held, scenario.OrderId, _document, [scenario.CouponId]);

            var pending = await held.Exchange.ExchangeAsync(scenario.Execution(key));

            await using var restarted = new OrderSliceHarness(_fixture, caller, emdAssociations: associations);
            var plan = (await restarted.ExchangePlans.FindAsync(pending.OperationId))!;
            var reassociation = Assert.Single(plan.Reassociations);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, pending.OperationStatus);
            Assert.True(plan.RequiresAncillaryReassociation);
            Assert.False(plan.IsAncillarySettled);
            Assert.Equal(ExchangeAncillaryState.Pending, plan.AncillaryState);
            Assert.Equal(_document, reassociation.EmdDocumentNumber);
            Assert.Equal(scenario.CouponId, reassociation.PredecessorTicketCouponId);
            Assert.NotNull(reassociation.TargetSuccessorTicketCouponId);
            Assert.Equal(AncillaryExchangeDisposition.ReassociateExisting, reassociation.Disposition);
            Assert.False(string.IsNullOrWhiteSpace(reassociation.DecisionReference));
            Assert.False(string.IsNullOrWhiteSpace(reassociation.DecisionContextFingerprint));

            associations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            Register(restarted, scenario);

            var finalized = await restarted.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(pending.SuccessorElectronicTicketId, finalized.SuccessorElectronicTicketId);
            Assert.Single(associations.ObservedRequests);
            Assert.NotEmpty(associations.ObservedRecoveryKeys);
            Assert.Empty(restarted.AncillaryDispositions.ObservedRequests);
        }

        [Fact]
        public async Task Replaying_a_completed_exchange_never_moves_the_ancillary_again()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Equal(ExchangeAncillaryState.Confirmed, replay.AncillaryState);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.Single(harness.AncillaryDispositions.ObservedRequests);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
        }

        [Fact]
        public async Task An_unconfigured_association_source_keeps_the_reissue_and_leaves_the_ancillary_detached()
        {
            await using var harness = NewHarness(new UnconfiguredEmdAssociationProvider());
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(20295, refusal.Code);
            Assert.Equal(501, refusal.HttpStatus);
            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);
            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
        }

        // ------------------------------------------- what the aggregate refuses

        [Fact]
        public void A_standalone_ancillary_cannot_be_disassociated_or_reassociated()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Standalone, null);

            Assert.Equal(
                20300,
                Assert.Throws<BusinessException>(
                    () => document.DisassociateCouponByReissue(Detach(500), Ids, Clock)).Code);
            Assert.Equal(
                20300,
                Assert.Throws<BusinessException>(
                    () => document.ReassociateCoupon(Move(500, 900), Ids, Clock)).Code);
        }

        [Fact]
        public void An_ancillary_whose_association_already_moved_elsewhere_cannot_be_disassociated()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Associated, 500);

            Assert.Equal(
                20302,
                Assert.Throws<BusinessException>(
                    () => document.DisassociateCouponByReissue(Detach(501), Ids, Clock)).Code);
        }

        [Fact]
        public void An_ancillary_that_was_never_disassociated_by_this_reissue_cannot_be_reassociated()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Associated, 500);

            Assert.Equal(
                20302,
                Assert.Throws<BusinessException>(() => document.ReassociateCoupon(Move(500, 900), Ids, Clock)).Code);
        }

        [Fact]
        public void Disassociating_the_same_reissue_twice_changes_nothing()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Associated, 500);

            document.DisassociateCouponByReissue(Detach(500), Ids, Clock);

            var version = document.DocumentVersion;
            var history = document.Coupons.Single().AssociationChanges.Count;

            document.DisassociateCouponByReissue(Detach(500), Ids, Clock);

            Assert.Null(document.Coupons.Single().AssociatedTicketCouponId);
            Assert.Equal(version, document.DocumentVersion);
            Assert.Equal(history, document.Coupons.Single().AssociationChanges.Count);
        }

        [Fact]
        public void Reassociating_onto_the_coupon_it_already_sits_on_changes_nothing()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Associated, 500);

            document.DisassociateCouponByReissue(Detach(500), Ids, Clock);
            document.ReassociateCoupon(Move(500, 900), Ids, Clock);

            var version = document.DocumentVersion;
            var history = document.Coupons.Single().AssociationChanges.Count;

            document.ReassociateCoupon(Move(500, 900), Ids, Clock);

            Assert.Equal(900, document.Coupons.Single().AssociatedTicketCouponId);
            Assert.Equal(version, document.DocumentVersion);
            Assert.Equal(history, document.Coupons.Single().AssociationChanges.Count);
        }

        [Fact]
        public void A_disassociated_ancillary_is_not_claimed_by_another_operation()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Associated, 500);

            document.DisassociateCouponByReissue(Detach(500), Ids, Clock);

            Assert.Equal(
                20302,
                Assert.Throws<BusinessException>(
                    () => document.ReassociateCoupon(Move(500, 900) with { OperationId = 12 }, Ids, Clock)).Code);
        }

        [Fact]
        public void A_disassociated_ancillary_still_completes_its_own_reissue_after_a_replay()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Associated, 500);

            document.DisassociateCouponByReissue(Detach(500), Ids, Clock);

            Assert.True(document.PermitsReassociation(1, 11, 900));
            Assert.True(document.PermitsDisassociation(1, 11, 500));

            document.ReassociateCoupon(Move(500, 900), Ids, Clock);

            Assert.True(document.PermitsDisassociation(1, 11, 500));
            Assert.Equal(
                [
                    EmdCouponAssociationChangeKind.Associated,
                    EmdCouponAssociationChangeKind.DisassociatedByReissue,
                    EmdCouponAssociationChangeKind.Reassociated
                ],
                document.Coupons.Single().AssociationChanges.Select(change => change.Kind).ToList());
        }

        private async Task<BusinessException> RefusedAsync(Action<OrderSliceHarness> arrange)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            arrange(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Empty(harness.EmdAssociations.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(ElectronicTicketStatus.Issued, ticket.StatusSummary);
            Assert.Equal(scenario.CommercialVersion, after.CommercialVersion);
            Assert.Equal(scenario.CouponId, coupon.AssociatedTicketCouponId);
            Assert.DoesNotContain(
                coupon.AssociationChanges,
                change => change.Kind != EmdCouponAssociationChangeKind.Associated);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);

            return refusal;
        }

        private async Task<ElectronicTicket> SuccessorAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
            => (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId);

        private static ElectronicMiscDocument Ancillary(
            ElectronicMiscDocumentType type,
            long? associatedTicketCouponId)
            => ElectronicMiscDocument.Issue(
                Ids.NewId(),
                1,
                7,
                11,
                NewDocumentNumber(),
                type,
                "A",
                77,
                null,
                DocumentAuthority.Local,
                1,
                [new EmdCouponIssuance(
                    EmdCouponPurpose.Fee,
                    "0DF",
                    50_000m,
                    [],
                    PricingLineId: 31,
                    AssociatedTicketCouponId: associatedTicketCouponId)],
                Ids,
                Clock);

        private static EmdCouponDisassociation Detach(long predecessorTicketCouponId)
            => new(1, predecessorTicketCouponId, "T9200001", 2, 11, "ANC-DECISION-1", "DOCX-1");

        private static EmdCouponReassociation Move(long predecessorTicketCouponId, long successorTicketCouponId)
            => new(
                1,
                predecessorTicketCouponId,
                "T9200001",
                2,
                successorTicketCouponId,
                "T9200099",
                2,
                11,
                "ANC-DECISION-1",
                "ASSOC-1");

        private static SequentialIdGenerator Ids { get; } = new();

        private static TestClock Clock { get; } = new();

        private static string NewDocumentNumber() => $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private OrderSliceHarness NewHarness(UnconfiguredEmdAssociationProvider associations)
            => new(_fixture, Caller(), unconfiguredEmdAssociations: associations);

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7432, $"emda-{Guid.NewGuid():N}");
    }
}
