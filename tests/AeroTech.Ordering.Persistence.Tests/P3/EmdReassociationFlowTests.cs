using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Domain._Shared.Contracts;
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

        // ---------------------------------------------------------------- U-W. the settled move

        [Fact]
        public async Task U_a_confirmed_reassociation_moves_the_ancillary_onto_the_successor_coupon()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var successor = (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId);
            var successorCoupon = successor.Coupons.Single();
            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var moved = ancillary.Coupons.Single();
            var projected = Assert.Single(outcome.Ancillaries);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeDocumentOutcome.Exchanged, outcome.DocumentOutcome);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(AncillaryExchangeDisposition.ReassociateExisting, projected.Disposition);
            Assert.Equal(ExchangeAncillaryState.Confirmed, projected.State);
            Assert.Equal(_document, projected.EmdDocumentNumber);
            Assert.Equal(1, projected.EmdCouponNumber);
            Assert.False(string.IsNullOrWhiteSpace(projected.ProviderReference));
            Assert.False(string.IsNullOrWhiteSpace(projected.DecisionReference));

            Assert.Equal(successorCoupon.Id, moved.AssociatedTicketCouponId);
            Assert.NotEqual(scenario.CouponId, moved.AssociatedTicketCouponId);
            Assert.Equal(EmdCouponStatus.OpenForUse, moved.Status);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
        }

        [Fact]
        public async Task V_the_move_is_requested_in_accountable_document_terms_only()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var request = Assert.Single(harness.EmdAssociations.ObservedRequests);
            var decision = Assert.Single(harness.AncillaryDispositions.ObservedRequests);

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
            Assert.Equal(decision.OperationId, request.OperationId);
            Assert.Contains($"{_document}:1", request.OperationKey);
            Assert.Contains(outcome.OperationId.ToString(), request.OperationKey);
        }

        [Fact]
        public async Task W_the_move_happens_only_after_the_money_has_settled()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await AddCollectAsync(_fixture, setup, harness, [1]);
            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

            await AttachAncillaryAsync(
                _fixture, setup, scenario.OrderId, _document, [ticket.Coupons.First().Id]);

            var outcome = await harness.Exchange.ExchangeAsync(
                scenario.FundedExecution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeFundingState.Captured, outcome.FundingState);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.NotEmpty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
        }

        // ---------------------------------------------------------------- X-Z. the unsettled move

        [Fact]
        public async Task X_a_refused_move_never_creates_the_successor_and_needs_reconciliation()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.ReassociateOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.Equal(ExchangeAncillaryState.Rejected, outcome.AncillaryState);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Equal(scenario.CouponId, ancillary.Coupons.Single().AssociatedTicketCouponId);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task Y_an_unresolved_move_is_read_back_and_never_dispatched_again(
            ProviderOperationOutcome unresolved)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.ReassociateOutcome = unresolved;
            harness.EmdAssociations.RecoveryOutcome = unresolved;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Pending, replay.AncillaryState);
            Assert.False(replay.RequiresReconciliation);
            Assert.Null(replay.SuccessorElectronicTicketId);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.NotEmpty(harness.EmdAssociations.ObservedRecoveryKeys);
            Assert.Equal(scenario.CouponId, ancillary.Coupons.Single().AssociatedTicketCouponId);
            Assert.Equal(ClaimConflict, await SecondOperationCodeAsync(harness, scenario));
        }

        [Fact]
        public async Task Z_an_unresolved_move_that_resolves_on_read_back_finalizes_once()
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

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Equal(first.OperationId, finalized.OperationId);
            Assert.Equal(ExchangeAncillaryState.Confirmed, finalized.AncillaryState);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Exchange);
            Assert.NotEqual(scenario.CouponId, ancillary.Coupons.Single().AssociatedTicketCouponId);
        }

        // ---------------------------------------------------------------- AA-AC. evidence the provider cannot prove

        [Fact]
        public async Task AA_a_move_the_caller_never_saw_is_read_back_instead_of_repeated()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.ThrowAfterDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(key)));

            harness.EmdAssociations.ThrowAfterDispatch = false;
            harness.EmdAssociations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.Single(harness.EmdAssociations.DispatchedKeys);
            Assert.Single(ancillary.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
        }

        [Fact]
        public async Task AB_a_confirmation_naming_another_coupon_needs_reconciliation()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.ReportedAssociatedCouponNumber = 9;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.True(outcome.RequiresReconciliation);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Equal(scenario.CouponId, ancillary.Coupons.Single().AssociatedTicketCouponId);
            Assert.False(string.IsNullOrWhiteSpace(Assert.Single(outcome.Ancillaries).Detail));
        }

        [Fact]
        public async Task AC_a_confirmation_with_no_provider_reference_needs_reconciliation()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.OmitProviderReference = true;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Equal(scenario.CouponId, ancillary.Coupons.Single().AssociatedTicketCouponId);
        }

        [Fact]
        public async Task AD_a_confirmation_naming_another_ancillary_document_needs_reconciliation()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.EmdAssociations.ReportedEmdDocumentNumber = NewDocumentNumber();

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Null(outcome.SuccessorElectronicTicketId);
            Assert.Equal(
                scenario.CouponId,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().AssociatedTicketCouponId);
        }

        // ---------------------------------------------------------------- AE-AG. more than one ancillary

        [Fact]
        public async Task AE_every_affected_ancillary_moves_under_its_own_stable_key()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var successorCouponId = (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId)
                .Coupons.Single().Id;

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
        public async Task AF_the_affected_ancillaries_are_settled_in_a_deterministic_order()
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
            Assert.Equal(
                ordered,
                outcome.Ancillaries.Select(ancillary => ancillary.EmdDocumentNumber).ToList());
        }

        [Fact]
        public async Task AG_one_unresolved_ancillary_holds_the_exchange_without_repeating_the_settled_one()
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
            Assert.Null(held.SuccessorElectronicTicketId);
            Assert.Equal(2, harness.EmdAssociations.ObservedRequests.Count);

            harness.EmdAssociations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var finalized = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var successorCouponId = (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == finalized.SuccessorElectronicTicketId)
                .Coupons.Single().Id;

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
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

        // ---------------------------------------------------------------- AH-AL. the durable record

        [Fact]
        public async Task AH_the_association_history_records_where_the_ancillary_came_from_and_went()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var predecessorCoupon = predecessor.Coupons.Single(coupon => coupon.Id == scenario.CouponId);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var history = ancillary.Coupons.Single().AssociationChanges;
            var successorCouponId = (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId)
                .Coupons.Single().Id;

            Assert.Equal(
                [
                    EmdCouponAssociationChangeKind.Associated,
                    EmdCouponAssociationChangeKind.DisassociatedByReissue,
                    EmdCouponAssociationChangeKind.Reassociated
                ],
                history.Select(change => change.Kind).ToList());
            Assert.Equal([1, 2, 3], history.Select(change => change.Sequence).ToList());

            var disassociated = history[1];
            var reassociated = history[2];

            Assert.Equal(scenario.CouponId, disassociated.PreviousTicketCouponId);
            Assert.Equal(predecessor.DocumentNumber, disassociated.PreviousDocumentNumber);
            Assert.Equal(predecessorCoupon.CouponNumber, disassociated.PreviousCouponNumber);
            Assert.Null(disassociated.CurrentTicketCouponId);
            Assert.Equal(outcome.OperationId, disassociated.OperationId);

            Assert.Equal(scenario.CouponId, reassociated.PreviousTicketCouponId);
            Assert.Equal(successorCouponId, reassociated.CurrentTicketCouponId);
            Assert.Equal(outcome.SuccessorDocumentNumber, reassociated.CurrentDocumentNumber);
            Assert.False(string.IsNullOrWhiteSpace(reassociated.DecisionReference));
            Assert.False(string.IsNullOrWhiteSpace(reassociated.ProviderReference));
        }

        [Fact]
        public async Task AI_the_moved_ancillary_document_is_versioned_exactly_once()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var attached = await AttachAncillaryAsync(
                _fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            var before = attached.DocumentVersion;

            await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(before + 1, ancillary.DocumentVersion);
        }

        [Fact]
        public async Task AJ_the_accepted_ancillary_plan_survives_a_process_restart()
        {
            var caller = Caller();
            var associations = new DeterministicEmdAssociationAdapter
            {
                ReassociateOutcome = ProviderOperationOutcome.Pending,
                RecoveryOutcome = ProviderOperationOutcome.Pending
            };

            await using var setup = NewHarness();
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
            Assert.Equal(1, reassociation.EmdCouponNumber);
            Assert.Equal(scenario.CouponId, reassociation.PredecessorTicketCouponId);
            Assert.NotNull(reassociation.TargetSuccessorTicketCouponId);
            Assert.Equal(AncillaryExchangeDisposition.ReassociateExisting, reassociation.Disposition);
            Assert.False(string.IsNullOrWhiteSpace(reassociation.DecisionReference));

            associations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            Register(restarted, scenario);

            var finalized = await restarted.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(associations.ObservedRequests);
            Assert.NotEmpty(associations.ObservedRecoveryKeys);
            Assert.Empty(restarted.AncillaryDispositions.ObservedRequests);
            Assert.NotEqual(
                scenario.CouponId,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().AssociatedTicketCouponId);
        }

        [Fact]
        public async Task AK_replaying_a_completed_exchange_never_moves_the_ancillary_again()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var key = NewKey();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Equal(ExchangeAncillaryState.Confirmed, replay.AncillaryState);
            Assert.Single(replay.Ancillaries);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
            Assert.Single(harness.AncillaryDispositions.ObservedRequests);
            Assert.Single(
                ancillary.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.Reassociated);
            Assert.Equal(first.SuccessorDocumentNumber, replay.SuccessorDocumentNumber);
        }

        [Fact]
        public async Task AL_an_unconfigured_association_source_keeps_the_plan_and_moves_nothing()
        {
            await using var harness = NewHarness(new UnconfiguredEmdAssociationProvider());
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(20295, refusal.Code);
            Assert.Equal(501, refusal.HttpStatus);
            Assert.Equal(scenario.CouponId, ancillary.Coupons.Single().AssociatedTicketCouponId);
            Assert.Empty((await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).Exchanges);
            Assert.DoesNotContain(
                (await ReloadAsync(_fixture, scenario.OrderId)).Changes,
                change => change.ChangeType == OrderChangeType.Exchange);
        }

        // ---------------------------------------------------------------- AM-AO. what the aggregate refuses

        [Fact]
        public void AM_a_standalone_ancillary_cannot_be_reassociated()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Standalone, null);

            var refusal = Assert.Throws<BusinessException>(
                () => document.ReassociateCoupon(Move(document, 500, 900), Ids, Clock));

            Assert.Equal(20300, refusal.Code);
        }

        [Fact]
        public void AN_an_ancillary_whose_association_already_moved_elsewhere_is_refused()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Associated, 500);

            var refusal = Assert.Throws<BusinessException>(
                () => document.ReassociateCoupon(Move(document, 501, 900), Ids, Clock));

            Assert.Equal(20302, refusal.Code);
        }

        [Fact]
        public void AO_moving_an_ancillary_that_already_sits_on_the_successor_changes_nothing()
        {
            var document = Ancillary(ElectronicMiscDocumentType.Associated, 500);

            document.ReassociateCoupon(Move(document, 500, 900), Ids, Clock);

            var versionAfterFirst = document.DocumentVersion;
            var historyAfterFirst = document.Coupons.Single().AssociationChanges.Count;

            document.ReassociateCoupon(Move(document, 500, 900), Ids, Clock);

            Assert.Equal(900, document.Coupons.Single().AssociatedTicketCouponId);
            Assert.Equal(versionAfterFirst, document.DocumentVersion);
            Assert.Equal(historyAfterFirst, document.Coupons.Single().AssociationChanges.Count);
        }

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

        private static EmdCouponReassociation Move(
            ElectronicMiscDocument document,
            long predecessorTicketCouponId,
            long successorTicketCouponId)
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
