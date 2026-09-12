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
    public sealed class EmdExchangeToNewEmdFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document = NewDocumentNumber();
        private readonly string _secondDocument = NewDocumentNumber();

        public EmdExchangeToNewEmdFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------- happy paths

        [Fact]
        public async Task G3H1_an_associated_successor_is_issued_associated_and_the_predecessor_is_exchanged()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var successor = await AncillaryAsync(_fixture, scenario.OrderId, SuccessorOf(_document));
            var sourceCoupon = source.Coupons.Single();
            var successorCoupon = successor.Coupons.Single();
            var successorTicket = await SuccessorTicketAsync(scenario, outcome);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);

            Assert.Equal(EmdCouponStatus.Exchanged, sourceCoupon.Status);
            Assert.Null(sourceCoupon.AssociatedTicketCouponId);
            Assert.Equal(ElectronicMiscDocumentStatus.Exchanged, source.StatusSummary);

            Assert.Equal(ElectronicMiscDocumentType.Associated, successor.Type);
            Assert.Equal(EmdCouponStatus.OpenForUse, successorCoupon.Status);
            Assert.Equal(successorTicket.Coupons.Single().Id, successorCoupon.AssociatedTicketCouponId);

            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
        }

        [Fact]
        public async Task G3H2_a_source_approved_standalone_successor_carries_no_ticket_association()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeSuccessorType = ElectronicMiscDocumentType.Standalone;
            harness.AncillaryDispositions.ExchangeSuccessorPurpose = EmdCouponPurpose.ResidualValue;
            harness.AncillaryDispositions.ExchangeSuccessorExternalValueReference = "RESIDUAL-VALUE-REF";

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var successor = await AncillaryAsync(_fixture, scenario.OrderId, SuccessorOf(_document));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ElectronicMiscDocumentType.Standalone, successor.Type);
            Assert.Null(successor.Coupons.Single().AssociatedTicketCouponId);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        [Fact]
        public async Task G3H3_one_group_of_two_source_coupons_is_one_provider_act_and_one_consequence()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, setup, roundTrip: true);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var coupons = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).ToList();

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [coupons[0].Id, coupons[1].Id]);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1, 2]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ExchangeToNewEmd;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var successor = await AncillaryAsync(_fixture, scenario.OrderId, SuccessorOf(_document));
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.OperationId == outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Equal([1, 2], Assert.Single(harness.EmdExchanges.ObservedRequests).SourceCouponNumbers);
            Assert.All(source.Coupons, coupon => Assert.Equal(EmdCouponStatus.Exchanged, coupon.Status));
            Assert.Equal(2, successor.Coupons.Count);
            Assert.Equal(2, after.PriceConsequencesOf(change.Id).Count);
        }

        [Fact]
        public async Task G3H4_two_independent_groups_own_one_provider_act_and_one_sequence_each()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);

            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ExchangeToNewEmd;
            harness.AncillaryDispositions.ExchangeGroupByCoupon[AncillaryKey(_document, 1)] = "EMDX-A";
            harness.AncillaryDispositions.ExchangeGroupByCoupon[AncillaryKey(_secondDocument, 1)] = "EMDX-B";

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.OperationId == outcome.OperationId);
            var consequences = after.PriceConsequencesOf(change.Id);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(2, harness.EmdExchanges.ObservedRequests.Count);
            Assert.Equal(3, consequences.Count);
            Assert.Equal(
                consequences.Count,
                consequences.Select(set => set.FinancialSequence).Distinct().Count());
            Assert.Equal(
                2,
                (await AncillariesAsync(_fixture, scenario.OrderId))
                    .Count(document => document.StatusSummary == ElectronicMiscDocumentStatus.Exchanged));
        }

        [Fact]
        public async Task G3H5_refund_reassociation_and_emd_exchange_settle_independently()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var third = NewDocumentNumber();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, third, [scenario.CouponId]);

            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_document, 1)] =
                AncillaryExchangeDisposition.Refund;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(_secondDocument, 1)] =
                AncillaryExchangeDisposition.ExchangeToNewEmd;
            harness.AncillaryDispositions.DispositionByCoupon[AncillaryKey(third, 1)] =
                AncillaryExchangeDisposition.ReassociateExisting;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var refunded = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var exchanged = await AncillaryAsync(_fixture, scenario.OrderId, _secondDocument);
            var moved = await AncillaryAsync(_fixture, scenario.OrderId, third);
            var successorTicket = await SuccessorTicketAsync(scenario, outcome);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(EmdCouponStatus.Refunded, refunded.Coupons.Single().Status);
            Assert.Equal(EmdCouponStatus.Exchanged, exchanged.Coupons.Single().Status);
            Assert.Equal(EmdCouponStatus.OpenForUse, moved.Coupons.Single().Status);
            Assert.Equal(successorTicket.Coupons.Single().Id, moved.Coupons.Single().AssociatedTicketCouponId);
            Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
        }

        [Fact]
        public async Task G3H6_only_the_approved_coupon_of_a_multi_coupon_source_is_exchanged()
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
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ExchangeToNewEmd;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                source.Coupons.Single(coupon => coupon.CouponNumber == 2).Status);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                source.Coupons.Single(coupon => coupon.CouponNumber == 1).Status);
            Assert.NotEqual(ElectronicMiscDocumentStatus.Exchanged, source.StatusSummary);
            Assert.Equal([2], Assert.Single(harness.EmdExchanges.ObservedRequests).SourceCouponNumbers);
        }

        // ---------------------------------------------- acceptance and source validation

        [Theory]
        [InlineData("no-terms", 20311)]
        [InlineData("no-group-ref", 20311)]
        [InlineData("no-successor-coupons", 20311)]
        [InlineData("no-source-reference", 20311)]
        [InlineData("no-funding-method", 20311)]
        [InlineData("ordering-derived", 20311)]
        [InlineData("no-pricing-evidence", 20311)]
        [InlineData("no-target-coupon", 20312)]
        [InlineData("unrelated-target-coupon", 20312)]
        [InlineData("conflicting-group-terms", 20312)]
        public async Task G3S1_incomplete_exchange_terms_fail_before_any_irreversible_work(
            string shape,
            int expectedCode)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var dispositions = harness.AncillaryDispositions;

            switch (shape)
            {
                case "no-terms":
                    dispositions.OmitExchangeTerms = true;
                    break;
                case "no-group-ref":
                    dispositions.OmitExchangeGroupRef = true;
                    break;
                case "no-successor-coupons":
                    dispositions.OmitExchangeSuccessorCoupons = true;
                    break;
                case "no-source-reference":
                    dispositions.OmitExchangeSourceReference = true;
                    break;
                case "no-funding-method":
                    dispositions.ExchangeAddCollect = 10_000m;
                    dispositions.ExchangeSuccessorValue = 60_000m;
                    dispositions.OmitExchangeFundingMethod = true;
                    break;
                case "ordering-derived":
                    dispositions.ReportSelfDerivedExchangePricing = true;
                    break;
                case "no-pricing-evidence":
                    dispositions.ExchangeAddCollect = 10_000m;
                    dispositions.OmitExchangePricingLines = true;
                    break;
                case "no-target-coupon":
                    dispositions.OmitExchangeTargetCoupon = true;
                    break;
                case "unrelated-target-coupon":
                    dispositions.ExchangeTargetCouponOverride = 9;
                    break;
                default:
                    await SecondCouponInOneGroupAsync(harness, scenario);
                    dispositions.ConflictExchangeGroupTerms = true;
                    break;
            }

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(expectedCode, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Equal(EmdCouponStatus.OpenForUse, source.Coupons.First().Status);
            Assert.Empty((await PredecessorAsync(scenario)).Exchanges);
        }

        [Fact]
        public async Task G3S2_an_unreconciled_monetary_shape_fails_before_any_irreversible_work()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeAddCollect = 25_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = 50_000m;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20314, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
        }

        [Fact]
        public async Task G3S3_a_group_spanning_two_source_documents_fails_before_any_irreversible_work()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ExchangeToNewEmd;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20312, refusal.Code);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
        }

        // ---------------------------------------------- provider lifecycle

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task G3P1_an_unresolved_exchange_materializes_nothing(ProviderOperationOutcome unresolved)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var key = NewKey();

            harness.EmdExchanges.ExchangeOutcome = unresolved;
            harness.EmdExchanges.RecoveryOutcome = unresolved;

            var held = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.NotNull(replay.SuccessorElectronicTicketId);
            Assert.Equal(EmdCouponStatus.OpenForUse, source.Coupons.Single().Status);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.DoesNotContain(
                after.PriceChangeSets,
                set => set.ChangeId == after.Changes.Single(change =>
                           change.OperationId == held.OperationId).Id
                       && set.FinancialSequence > scenario.FinancialSequence + 1);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.NotEmpty(harness.EmdExchanges.ObservedRecoveryKeys);
        }

        [Fact]
        public async Task G3P2_a_refused_exchange_keeps_the_reissue_and_materializes_no_successor()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.EmdExchanges.ExchangeOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var coupon = source.Coupons.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.Single(
                coupon.AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
        }

        [Fact]
        public async Task G3P3_a_request_that_never_left_ordering_exchanges_nothing_and_retries_once()
        {
            var caller = Caller();
            var exchanges = new DeterministicEmdExchangeAdapter { ThrowBeforeDispatch = true };

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdExchanges: exchanges);
            var scenario = await ExchangeScenarioAsync(crashed);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Empty(exchanges.DispatchedKeys);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);

            exchanges.ThrowBeforeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdExchanges: exchanges);
            Register(resumed, scenario);
            SetUpExchangeDisposition(resumed);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(exchanges.DispatchedKeys);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        [Fact]
        public async Task G3P4_an_exchange_the_caller_never_saw_is_recovered_and_never_repeated()
        {
            var caller = Caller();
            var exchanges = new DeterministicEmdExchangeAdapter { ThrowAfterDispatch = true };

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdExchanges: exchanges);
            var scenario = await ExchangeScenarioAsync(crashed);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            exchanges.ThrowAfterDispatch = false;
            exchanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdExchanges: exchanges);
            Register(resumed, scenario);
            SetUpExchangeDisposition(resumed);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var successor = await AncillaryAsync(_fixture, scenario.OrderId, SuccessorOf(_document));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(exchanges.ObservedRequests);
            Assert.Single(exchanges.DispatchedKeys);
            Assert.Equal(EmdCouponStatus.Exchanged, source.Coupons.Single().Status);
            Assert.Equal(successor.Id, source.Coupons.Single().ExchangeRecord!.SuccessorElectronicMiscDocumentId);
        }

        [Theory]
        [InlineData("no-successor")]
        [InlineData("wrong-type")]
        [InlineData("wrong-currency")]
        [InlineData("wrong-value")]
        [InlineData("wrong-sub-code")]
        [InlineData("no-association")]
        [InlineData("wrong-association")]
        [InlineData("no-provider-reference")]
        public async Task G3P5_a_contradictory_confirmation_reconciles_without_materializing(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var exchanges = harness.EmdExchanges;

            switch (shape)
            {
                case "no-successor":
                    exchanges.OmitSuccessor = true;
                    break;
                case "wrong-type":
                    exchanges.SuccessorTypeOverride = ElectronicMiscDocumentType.Standalone;
                    break;
                case "wrong-currency":
                    exchanges.SuccessorCurrencyOverride = 77;
                    break;
                case "wrong-value":
                    exchanges.SuccessorValueOverride = 999m;
                    break;
                case "wrong-sub-code":
                    exchanges.SuccessorSubCodeOverride = "0ZZ";
                    break;
                case "no-association":
                    exchanges.OmitSuccessorAssociation = true;
                    break;
                case "wrong-association":
                    exchanges.SuccessorAssociatedCouponOverride = 9;
                    break;
                default:
                    exchanges.OmitProviderReference = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Single(harness.EmdExchanges.ObservedRequests);
        }

        [Fact]
        public async Task G3P6_an_operation_key_with_a_conflicting_intent_fails_closed()
        {
            var exchanges = new DeterministicEmdExchangeAdapter();
            var request = Contracts.EmdExchange.EmdExchangePortFixture.Request();

            await exchanges.ExchangeAsync(request);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => exchanges.ExchangeAsync(request with { SourceCouponNumbers = [7] }));

            Assert.Single(exchanges.DispatchedKeys);
        }

        // ---------------------------------------------- local replay and identity

        [Fact]
        public async Task G3R1_an_exact_confirmed_replay_creates_no_second_successor_or_consequence()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var before = await ReloadAsync(_fixture, scenario.OrderId);

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(before.PriceChangeSets.Count, after.PriceChangeSets.Count);
            Assert.Equal(before.PricingLines.Count, after.PricingLines.Count);
            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.Changes.Count, after.Changes.Count);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Single(
                harness.Events.Dispatched.OfType<OrderPricingChanged>(),
                raised => raised.Reason == PriceChangeReason.Exchange
                          && raised.SourcePricingReference == "ANC-EXCHANGE-SOURCE");
            Assert.Single(
                source.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
        }

        [Fact]
        public async Task G3R2_an_existing_document_number_with_conflicting_truth_reconciles()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(
                _fixture, setup, scenario.OrderId, SuccessorOf(_document), [(long?)null],
                ElectronicMiscDocumentType.Standalone);

            SetUpExchangeDisposition(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
        }

        [Theory]
        [InlineData(EmdCouponStatus.Refunded)]
        [InlineData(EmdCouponStatus.Void)]
        public async Task G3R3_a_source_coupon_that_turned_terminal_after_acceptance_fails_closed(
            EmdCouponStatus terminal)
        {
            var caller = Caller();
            var exchanges = new DeterministicEmdExchangeAdapter { ThrowBeforeDispatch = true };

            await using var crashed = new OrderSliceHarness(_fixture, caller, emdExchanges: exchanges);
            var scenario = await ExchangeScenarioAsync(crashed);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            var document = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            await SetAncillaryCouponStatusAsync(_fixture, document.Coupons.Single().Id, terminal);

            exchanges.ThrowBeforeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, emdExchanges: exchanges);
            Register(resumed, scenario);
            SetUpExchangeDisposition(resumed);

            var outcome = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Empty(exchanges.DispatchedKeys);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Equal(
                ElectronicTicketStatus.Exchanged,
                (await PredecessorAsync(scenario)).StatusSummary);
        }

        [Fact]
        public async Task G3R4_a_terminal_coupon_is_never_an_affected_ancillary_of_the_reissue()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            var document = await AttachAncillaryAsync(
                _fixture, setup, scenario.OrderId, _document, [scenario.CouponId]);

            await SetAncillaryCouponStatusAsync(
                _fixture, document.Coupons.Single().Id, EmdCouponStatus.Refunded);
            SetUpExchangeDisposition(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.NotRequired, outcome.AncillaryState);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.AncillaryDispositions.ObservedRequests);
            Assert.Equal(EmdCouponStatus.Refunded, after.Coupons.Single().Status);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
        }

        // ---------------------------------------------- monetary and coupled residual

        [Fact]
        public async Task G3M1_an_add_collect_group_guarantees_then_exchanges_then_captures()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeAddCollect = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = 60_000m;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(EmdCouponStatus.Exchanged, source.Coupons.Single().Status);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.True(
                harness.ExchangeFunding.ObservedGuarantees[0].OperationKey
                    .Contains("emd-exchange-guarantee", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        [InlineData(ProviderOperationOutcome.Rejected)]
        public async Task G3M2_a_failed_capture_never_rolls_the_exchange_back(ProviderOperationOutcome failure)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeAddCollect = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = 60_000m;
            harness.ExchangeFunding.CaptureOutcome = failure;
            harness.ExchangeFunding.CaptureRecoveryOutcome = failure;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.OperationId == outcome.OperationId);

            Assert.Equal(
                failure == ProviderOperationOutcome.Rejected
                    ? ServicingOperationStatus.NeedsReconciliation
                    : ServicingOperationStatus.AwaitingExternal,
                outcome.OperationStatus);
            Assert.Equal(EmdCouponStatus.Exchanged, source.Coupons.Single().Status);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(2, after.PriceConsequencesOf(change.Id).Count);
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
        }

        [Fact]
        public async Task G3M3_a_coupled_residual_document_is_materialized_in_the_same_checkpoint()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeResidual = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = 40_000m;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var documents = await AncillariesAsync(_fixture, scenario.OrderId);
            var residual = documents.Single(document =>
                document.DocumentNumber == $"{SuccessorOf(_document)}R");

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(3, documents.Count);
            Assert.Equal(ElectronicMiscDocumentType.Standalone, residual.Type);
            Assert.Equal(EmdCouponPurpose.ResidualValue, residual.Coupons.Single().Purpose);
            Assert.Equal(10_000m, residual.Coupons.Single().IssuanceValue);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        [Fact]
        public async Task G3M4_a_missing_coupled_residual_reconciles_without_redispatching_the_exchange()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeResidual = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = 40_000m;
            harness.EmdExchanges.OmitCoupledResidual = true;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        [Fact]
        public async Task G3M5_an_unexpected_coupled_residual_reconciles_and_moves_no_value()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.EmdExchanges.ReportUnexpectedResidual = true;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        [Fact]
        public async Task G3M6_an_externally_fulfilled_residual_runs_after_document_truth_is_durable()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeResidual = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = 40_000m;
            harness.AncillaryDispositions.ExchangeResidualFulfillment = ResidualFulfillment.ExternalValue;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
        }

        // ---------------------------------------------- frozen invariants

        [Fact]
        public async Task G3F1_a_completed_exchange_replays_without_moving_a_document_or_any_money()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var key = NewKey();

            harness.AncillaryDispositions.ExchangeAddCollect = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = 60_000m;

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.True(replay.IsReplay);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.DocumentExchanges.ObservedRequests);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);
        }

        [Fact]
        public async Task G3F2_lineage_is_queryable_in_both_directions_and_bumps_the_version_once()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var key = NewKey();

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var successor = await AncillaryAsync(_fixture, scenario.OrderId, SuccessorOf(_document));
            var sourceCoupon = source.Coupons.Single();
            var successorCoupon = successor.Coupons.Single();

            Assert.Equal(successor.Id, sourceCoupon.ExchangeRecord!.SuccessorElectronicMiscDocumentId);
            Assert.Equal(successor.DocumentNumber, sourceCoupon.ExchangeRecord.SuccessorDocumentNumber);
            Assert.Equal(successorCoupon.CouponNumber, sourceCoupon.ExchangeRecord.SuccessorCouponNumber);
            Assert.Equal(outcome.OperationId, sourceCoupon.ExchangeRecord.OperationId);

            Assert.Equal(source.Id, successorCoupon.PredecessorElectronicMiscDocumentId);
            Assert.Equal(source.DocumentNumber, successorCoupon.PredecessorDocumentNumber);
            Assert.Equal(sourceCoupon.CouponNumber, successorCoupon.PredecessorCouponNumber);
            Assert.True(successorCoupon.ReplacesAnotherCoupon);

            Assert.Equal(3, source.DocumentVersion);

            await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(
                3,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).DocumentVersion);
        }

        [Fact]
        public async Task G3F3_an_exchanged_coupon_is_never_reassociated_refunded_or_exchanged_again()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var coupon = source.Coupons.Single();

            Assert.Equal(EmdCouponStatus.Exchanged, coupon.Status);
            Assert.False(source.PermitsReassociation(coupon.CouponNumber, 999L, 12345L));
            Assert.False(source.PermitsRefund(coupon.CouponNumber, 999L));
            Assert.False(source.PermitsExchange(coupon.CouponNumber, 999L));
            Assert.True(source.PermitsExchange(coupon.CouponNumber, outcome.OperationId));
            Assert.Empty(source.CouponsAssociatedWith([scenario.CouponId]));
        }

        // ---------------------------------------------- helpers

        private async Task<ExchangeScenario> ExchangeScenarioAsync(OrderSliceHarness harness)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            SetUpExchangeDisposition(harness);

            return scenario;
        }

        private static void SetUpExchangeDisposition(OrderSliceHarness harness)
            => harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ExchangeToNewEmd;

        private async Task SecondCouponInOneGroupAsync(OrderSliceHarness harness, ExchangeScenario scenario)
        {
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);

            harness.AncillaryDispositions.ExchangeGroupByCoupon[AncillaryKey(_document, 1)] = "EMDX-SHARED";
            harness.AncillaryDispositions.ExchangeGroupByCoupon[AncillaryKey(_secondDocument, 1)] = "EMDX-SHARED";
        }

        private async Task<ElectronicTicket> PredecessorAsync(ExchangeScenario scenario)
            => await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

        private async Task<ElectronicTicket> SuccessorTicketAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
            => (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId);

        private static string SuccessorOf(string documentNumber) => $"{documentNumber}X";

        private static string NewDocumentNumber() => $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7436, $"emdx-{Guid.NewGuid():N}");
    }
}
