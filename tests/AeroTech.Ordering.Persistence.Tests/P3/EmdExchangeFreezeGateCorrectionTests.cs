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
    public sealed class EmdExchangeFreezeGateCorrectionTests
    {
        private const decimal SourceValue = 50_000m;

        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document = NewDocumentNumber();
        private readonly string _secondDocument = NewDocumentNumber();

        public EmdExchangeFreezeGateCorrectionTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------- C1-C4. refund-due is executed

        [Fact]
        public async Task C1_a_confirmed_refund_due_moves_value_once_and_completes()
        {
            await using var harness = NewHarness();
            var scenario = await RefundDueScenarioAsync(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var request = Assert.Single(harness.RefundValues.ObservedRequests);
            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(EmdCouponStatus.Exchanged, source.Coupons.Single().Status);
            Assert.Contains("emd-exchange-refund", request.OperationKey, StringComparison.Ordinal);
            Assert.Equal(10_000m, request.ApprovedAmount);
            Assert.Equal(SuccessorOf(_document), request.SuccessorDocumentNumber);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task C2_an_unresolved_refund_due_holds_the_operation_and_keeps_emd_truth(
            ProviderOperationOutcome unresolved)
        {
            await using var harness = NewHarness();
            var scenario = await RefundDueScenarioAsync(harness);
            var key = NewKey();

            harness.RefundValues.RequestOutcome = unresolved;
            harness.RefundValues.RecoveryOutcome = unresolved;

            var held = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.OperationId == held.OperationId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(2, after.PriceConsequencesOf(change.Id).Count);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
        }

        [Theory]
        [InlineData("rejected")]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        [InlineData("wrong-disposition")]
        [InlineData("no-reference")]
        public async Task C3_a_refused_or_contradictory_refund_due_reconciles_without_undoing_anything(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await RefundDueScenarioAsync(harness);

            switch (shape)
            {
                case "rejected":
                    harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Rejected;
                    break;
                case "wrong-amount":
                    harness.RefundValues.AmountOverride = 9_999m;
                    break;
                case "wrong-currency":
                    harness.RefundValues.CurrencyOverride = 77;
                    break;
                case "wrong-disposition":
                    harness.RefundValues.DispositionOverride = "SomewhereElse";
                    break;
                default:
                    harness.RefundValues.OmitValueMovementReference = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.OperationId == outcome.OperationId);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(2, after.PriceConsequencesOf(change.Id).Count);
            Assert.Equal(ElectronicTicketStatus.Exchanged, (await PredecessorAsync(scenario)).StatusSummary);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
        }

        [Fact]
        public async Task C4_add_collect_and_refund_due_run_guarantee_exchange_capture_then_refund()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeAddCollect = 20_000m;
            harness.AncillaryDispositions.ExchangeRefundDue = 5_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue + 15_000m;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        // ---------------------------------------------- C5. frozen monetary shapes

        [Fact]
        public async Task C5a_add_collect_with_a_residual_is_a_supported_shape()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeAddCollect = 20_000m;
            harness.AncillaryDispositions.ExchangeResidual = 5_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue + 15_000m;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Equal(3, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
        }

        [Theory]
        [InlineData("refund-and-residual")]
        [InlineData("all-three")]
        [InlineData("non-positive-collection")]
        [InlineData("non-positive-refund")]
        [InlineData("wrong-currency-refund")]
        [InlineData("blank-refund-disposition")]
        public async Task C5b_an_unsupported_monetary_shape_fails_before_any_irreversible_work(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var dispositions = harness.AncillaryDispositions;

            switch (shape)
            {
                case "refund-and-residual":
                    dispositions.ExchangeRefundDue = 5_000m;
                    dispositions.ExchangeResidual = 5_000m;
                    dispositions.ExchangeSuccessorValue = SourceValue - 10_000m;
                    break;
                case "all-three":
                    dispositions.ExchangeAddCollect = 20_000m;
                    dispositions.ExchangeRefundDue = 5_000m;
                    dispositions.ExchangeResidual = 5_000m;
                    break;
                case "non-positive-collection":
                    dispositions.ExchangeAddCollect = 0m;
                    break;
                case "non-positive-refund":
                    dispositions.ExchangeRefundDue = 0m;
                    break;
                case "wrong-currency-refund":
                    dispositions.ExchangeRefundDue = 5_000m;
                    dispositions.ExchangeRefundCurrencyOverride = 77;
                    break;
                default:
                    dispositions.ExchangeRefundDue = 5_000m;
                    dispositions.OmitExchangeRefundDisposition = true;
                    break;
            }

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20312, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            await AssertNothingIrreversibleHappenedAsync(harness, scenario);
        }

        // ---------------------------------------------- C6. a coupled residual is EMD only

        [Theory]
        [InlineData(ResidualInstrumentKind.Mco)]
        [InlineData(ResidualInstrumentKind.Voucher)]
        [InlineData(ResidualInstrumentKind.TravelCredit)]
        [InlineData(ResidualInstrumentKind.Other)]
        [InlineData(ResidualInstrumentKind.Unknown)]
        public async Task C6_a_document_coupled_residual_that_is_not_an_emd_fails_before_any_irreversible_work(
            ResidualInstrumentKind instrument)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeResidual = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue - 10_000m;
            harness.AncillaryDispositions.ExchangeResidualInstrument = instrument;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20312, refusal.Code);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            await AssertNothingIrreversibleHappenedAsync(harness, scenario);
        }

        // ---------------------------------------------- C7. coupled residual evidence fails closed

        [Theory]
        [InlineData("wrong-instrument")]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        [InlineData("no-reason-for-issuance")]
        public async Task C7_contradictory_coupled_residual_evidence_reconciles_without_redispatch(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeResidual = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue - 10_000m;

            switch (shape)
            {
                case "wrong-instrument":
                    harness.EmdExchanges.ResidualInstrumentOverride = ResidualInstrumentKind.Voucher;
                    break;
                case "wrong-amount":
                    harness.EmdExchanges.ResidualAmountOverride = 1m;
                    break;
                case "wrong-currency":
                    harness.EmdExchanges.ResidualCurrencyOverride = 77;
                    break;
                default:
                    harness.EmdExchanges.OmitResidualReasonForIssuance = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        // ---------------------------------------------- C8. external residual evidence fails closed

        [Theory]
        [InlineData("no-provider-reference")]
        [InlineData("no-instrument-reference")]
        [InlineData("wrong-instrument")]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        public async Task C8_contradictory_external_residual_evidence_reconciles_and_keeps_emd_truth(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeResidual = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue - 10_000m;
            harness.AncillaryDispositions.ExchangeResidualFulfillment = ResidualFulfillment.ExternalValue;

            switch (shape)
            {
                case "no-provider-reference":
                    harness.ExchangeResiduals.OmitProviderReference = true;
                    break;
                case "no-instrument-reference":
                    harness.ExchangeResiduals.OmitInstrumentReference = true;
                    break;
                case "wrong-instrument":
                    harness.ExchangeResiduals.InstrumentOverride = ResidualInstrumentKind.Voucher;
                    break;
                case "wrong-amount":
                    harness.ExchangeResiduals.AmountOverride = 1m;
                    break;
                default:
                    harness.ExchangeResiduals.CurrencyOverride = 77;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(
                EmdCouponStatus.Exchanged,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeResiduals.ObservedRequests);
        }

        // ---------------------------------------------- C9-C10. provider coupon numbers are authoritative

        [Fact]
        public async Task C9_non_default_provider_coupon_numbers_are_persisted_exactly_in_both_directions()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.EmdExchanges.SuccessorCouponNumbersOverride = [7];

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var successor = await AncillaryAsync(_fixture, scenario.OrderId, SuccessorOf(_document));
            var successorCoupon = successor.Coupons.Single();
            var sourceCoupon = source.Coupons.Single();

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(7, successorCoupon.CouponNumber);
            Assert.Equal(7, sourceCoupon.ExchangeRecord!.SuccessorCouponNumber);
            Assert.Equal(sourceCoupon.CouponNumber, successorCoupon.PredecessorCouponNumber);
            Assert.Equal(successor.Id, sourceCoupon.ExchangeRecord.SuccessorElectronicMiscDocumentId);
        }

        [Theory]
        [InlineData("duplicate")]
        [InlineData("non-positive")]
        public async Task C10_malformed_provider_coupon_numbers_reconcile_without_materializing(string shape)
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
            harness.EmdExchanges.SuccessorCouponNumbersOverride =
                shape == "duplicate" ? [1, 1] : [0, 2];

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.All(
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons,
                coupon => Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status));
        }

        // ---------------------------------------------- C11. the mapping cardinality is frozen at one to one

        [Fact]
        public async Task C11_a_source_to_successor_cardinality_mismatch_fails_before_ticket_exchange()
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
            harness.AncillaryDispositions.MergeExchangeGroupToOneSuccessor = true;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20312, refusal.Code);
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
        }

        // ---------------------------------------------- C12. an existing successor number is exact replay identity

        [Fact]
        public async Task C12_an_existing_successor_number_from_another_operation_is_never_accepted_as_replay()
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
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
            Assert.Equal(
                ElectronicTicketStatus.Exchanged,
                (await PredecessorAsync(scenario)).StatusSummary);
        }

        // ---------------------------------------------- C13. association and beneficiary evidence

        [Theory]
        [InlineData("associated-without-ticket-document")]
        [InlineData("standalone-claiming-a-ticket")]
        [InlineData("wrong-beneficiary")]
        public async Task C13_incomplete_association_or_beneficiary_evidence_reconciles(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            switch (shape)
            {
                case "associated-without-ticket-document":
                    harness.EmdExchanges.OmitSuccessorTicketDocument = true;
                    break;
                case "standalone-claiming-a-ticket":
                    harness.AncillaryDispositions.ExchangeSuccessorType =
                        ElectronicMiscDocumentType.Standalone;
                    harness.AncillaryDispositions.ExchangeSuccessorPurpose = EmdCouponPurpose.ResidualValue;
                    harness.AncillaryDispositions.ExchangeSuccessorExternalValueReference = "RESIDUAL-REF";
                    harness.EmdExchanges.ForceSuccessorTicketDocument = true;
                    break;
                default:
                    harness.EmdExchanges.BeneficiaryOverride = 4242L;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Single(await AncillariesAsync(_fixture, scenario.OrderId));
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        // ---------------------------------------------- C14. deterministic request identity

        [Theory]
        [InlineData("source-pricing-reference")]
        [InlineData("residual-expected-instrument")]
        public async Task C14_the_same_key_with_a_changed_immutable_intent_fails_closed(string shape)
        {
            var exchanges = new DeterministicEmdExchangeAdapter();
            var request = Contracts.EmdExchange.EmdExchangePortFixture.ResidualRequest();

            await exchanges.ExchangeAsync(request);

            var changed = shape == "source-pricing-reference"
                ? request with { SourcePricingReference = "ANOTHER-SOURCE" }
                : request with
                {
                    Residual = request.Residual! with { ExpectedInstrument = ResidualInstrumentKind.Voucher }
                };

            await Assert.ThrowsAsync<InvalidOperationException>(() => exchanges.ExchangeAsync(changed));

            Assert.Single(exchanges.DispatchedKeys);
        }

        // ---------------------------------------------- C15. every group carries a commercial consequence

        [Fact]
        public async Task C15_an_even_exchange_without_pricing_evidence_fails_before_ticket_exchange()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.OmitExchangePricingLines = true;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            Assert.Equal(20311, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            await AssertNothingIrreversibleHappenedAsync(harness, scenario);
        }

        [Fact]
        public async Task C15b_an_even_exchange_owns_exactly_one_consequence_event_and_version_advance()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var change = Assert.Single(after.Changes, candidate => candidate.OperationId == outcome.OperationId);
            var consequences = after.PriceConsequencesOf(change.Id);
            var exchangeEvents = harness.Events.Dispatched
                .OfType<OrderPricingChanged>()
                .Where(raised => raised.SourcePricingReference == "ANC-EXCHANGE-SOURCE")
                .ToList();

            Assert.Equal(2, consequences.Count);
            Assert.Single(exchangeEvents);
            Assert.Equal(consequences[1].Id, exchangeEvents[0].PriceChangeSetId);
            Assert.Equal(change.Id, exchangeEvents[0].OrderChangeId);
            Assert.Equal(consequences[1].ExpectedCommercialVersion + 1, after.CommercialVersion);
        }

        // ---------------------------------------------- C16. exact replay

        [Fact]
        public async Task C16_an_exact_replay_of_a_full_monetary_group_moves_nothing_twice()
        {
            await using var harness = NewHarness();
            var scenario = await ExchangeScenarioAsync(harness);
            var key = NewKey();

            harness.AncillaryDispositions.ExchangeAddCollect = 20_000m;
            harness.AncillaryDispositions.ExchangeRefundDue = 5_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue + 15_000m;

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var before = await ReloadAsync(_fixture, scenario.OrderId);

            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var source = await AncillaryAsync(_fixture, scenario.OrderId, _document);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Single(harness.EmdExchanges.ObservedRequests);
            Assert.Single(harness.ExchangeFunding.ObservedCaptures);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Equal(2, (await AncillariesAsync(_fixture, scenario.OrderId)).Count);
            Assert.Equal(before.PriceChangeSets.Count, after.PriceChangeSets.Count);
            Assert.Equal(before.PricingLines.Count, after.PricingLines.Count);
            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.Changes.Count, after.Changes.Count);
            Assert.Equal(3, source.DocumentVersion);
            Assert.Single(
                source.Coupons.Single().AssociationChanges,
                history => history.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
        }

        // ---------------------------------------------- helpers

        private async Task<ExchangeScenario> ExchangeScenarioAsync(OrderSliceHarness harness)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            SetUpExchangeDisposition(harness);

            return scenario;
        }

        private async Task<ExchangeScenario> RefundDueScenarioAsync(OrderSliceHarness harness)
        {
            var scenario = await ExchangeScenarioAsync(harness);

            harness.AncillaryDispositions.ExchangeRefundDue = 10_000m;
            harness.AncillaryDispositions.ExchangeSuccessorValue = SourceValue - 10_000m;

            return scenario;
        }

        private static void SetUpExchangeDisposition(OrderSliceHarness harness)
            => harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ExchangeToNewEmd;

        private async Task AssertNothingIrreversibleHappenedAsync(
            OrderSliceHarness harness,
            ExchangeScenario scenario)
        {
            Assert.Empty(harness.EmdExchanges.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ExchangeFunding.ObservedGuarantees);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
            Assert.Empty((await PredecessorAsync(scenario)).Exchanges);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.First().Status);
        }

        private async Task<ElectronicTicket> PredecessorAsync(ExchangeScenario scenario)
            => await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);

        private static string SuccessorOf(string documentNumber) => $"{documentNumber}X";

        private static string NewDocumentNumber() => $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7436, $"emdxc-{Guid.NewGuid():N}");
    }
}
