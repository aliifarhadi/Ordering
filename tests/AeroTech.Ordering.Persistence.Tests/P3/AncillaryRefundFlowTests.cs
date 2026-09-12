using AeroTech.Framework.Core.Domain.Exceptions;
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
    public sealed class AncillaryRefundFlowTests
    {
        private readonly OrderingDatabaseFixture _fixture;
        private readonly string _document = NewDocumentNumber();
        private readonly string _secondDocument = NewDocumentNumber();

        public AncillaryRefundFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        // ---------------------------------------------- happy paths

        [Fact]
        public async Task G2H1_a_confirmed_refund_settles_the_document_then_the_value()
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var predecessor = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var coupon = ancillary.Coupons.Single();
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var projected = Assert.Single(outcome.Ancillaries);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(AncillaryExchangeDisposition.Refund, projected.Disposition);

            Assert.Equal(EmdCouponStatus.Refunded, coupon.Status);
            Assert.Null(coupon.AssociatedTicketCouponId);
            Assert.Equal(ElectronicMiscDocumentStatus.Refunded, ancillary.StatusSummary);
            Assert.NotNull(coupon.RefundRecord);
            Assert.Equal(outcome.OperationId, coupon.RefundRecord!.OperationId);
            Assert.Equal(RefundAmount, coupon.RefundRecord.ApprovedAmount);

            Assert.Equal(ElectronicTicketStatus.Exchanged, predecessor.StatusSummary);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Refund);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Refund);

            Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.EmdAssociations.ObservedRequests);
        }

        [Fact]
        public async Task G2H2_only_the_approved_coupon_of_a_multi_coupon_document_is_refunded()
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
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.Refund;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var refunded = ancillary.Coupons.Single(candidate => candidate.CouponNumber == 2);
            var untouched = ancillary.Coupons.Single(candidate => candidate.CouponNumber == 1);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(EmdCouponStatus.Refunded, refunded.Status);
            Assert.Equal(EmdCouponStatus.OpenForUse, untouched.Status);
            Assert.Equal(coupons[0].Id, untouched.AssociatedTicketCouponId);
            Assert.NotEqual(ElectronicMiscDocumentStatus.Refunded, ancillary.StatusSummary);
            Assert.Equal([2], Assert.Single(harness.DocumentRefunds.ObservedRefundRequests).CouponNumbers);
        }

        [Fact]
        public async Task G2H3_a_refund_and_a_reassociation_settle_independently_in_one_exchange()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);

            var refundTarget = new[] { _document, _secondDocument }.Order(StringComparer.Ordinal).First();
            harness.AncillaryDispositions.DispositionByCoupon[$"{refundTarget}:1"] =
                AncillaryExchangeDisposition.Refund;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var successorCouponId = (await SuccessorAsync(scenario, outcome)).Coupons.Single().Id;
            var refunded = await AncillaryAsync(_fixture, scenario.OrderId, refundTarget);
            var moved = (await AncillariesAsync(_fixture, scenario.OrderId))
                .Single(document => document.DocumentNumber != refundTarget);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ExchangeAncillaryState.Confirmed, outcome.AncillaryState);
            Assert.Equal(2, outcome.Ancillaries.Count);

            Assert.Equal(EmdCouponStatus.Refunded, refunded.Coupons.Single().Status);
            Assert.Null(refunded.Coupons.Single().AssociatedTicketCouponId);

            Assert.Equal(EmdCouponStatus.OpenForUse, moved.Coupons.Single().Status);
            Assert.Equal(successorCouponId, moved.Coupons.Single().AssociatedTicketCouponId);

            Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Single(harness.EmdAssociations.ObservedRequests);
        }

        [Fact]
        public async Task G2H4_multiple_refunds_settle_in_a_deterministic_order()
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);
            var ordered = new[] { _document, _secondDocument }.Order(StringComparer.Ordinal).ToList();

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _secondDocument, [scenario.CouponId]);
            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.Refund;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(
                ordered,
                harness.DocumentRefunds.ObservedRefundRequests.Select(request => request.DocumentNumber).ToList());
            Assert.All(
                await AncillariesAsync(_fixture, scenario.OrderId),
                document => Assert.Equal(EmdCouponStatus.Refunded, document.Coupons.Single().Status));
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Refund);
        }

        [Fact]
        public async Task G2H5_a_refunded_service_ancillary_is_no_longer_deliverable()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, setup);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var coupon = ticket.Coupons.First();

            await AttachAncillaryAsync(
                _fixture, setup, issued.OrderId, _document, [coupon.Id],
                deliveringOrderServiceId: coupon.CurrentOrderServiceId);

            var scenario = await QuotedAsync(_fixture, harness, issued, [1]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.Refund;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var after = await ReloadAsync(_fixture, scenario.OrderId);
            var service = after.OrderServices.Single(candidate => candidate.Id == coupon.CurrentOrderServiceId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(OrderServiceFinancialStatus.Refunded, service.FinancialStatus);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Refund);
        }

        // ---------------------------------------------- source validation

        [Theory]
        [InlineData("no-terms")]
        [InlineData("no-amount")]
        [InlineData("wrong-currency")]
        [InlineData("no-disposition")]
        [InlineData("no-source-reference")]
        [InlineData("no-pricing-lines")]
        public async Task G2S1_a_refund_without_source_approved_economics_fails_before_any_exchange_work(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.Refund;

            switch (shape)
            {
                case "no-terms":
                    harness.AncillaryDispositions.OmitRefundTerms = true;
                    break;
                case "no-amount":
                    harness.AncillaryDispositions.RefundAmountOverride = 0m;
                    break;
                case "wrong-currency":
                    harness.AncillaryDispositions.RefundCurrencyOverride = 77;
                    break;
                case "no-disposition":
                    harness.AncillaryDispositions.OmitRefundDisposition = true;
                    break;
                case "no-source-reference":
                    harness.AncillaryDispositions.OmitRefundSourceReference = true;
                    break;
                default:
                    harness.AncillaryDispositions.OmitRefundPricingLines = true;
                    break;
            }

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Exchange.ExchangeAsync(scenario.Execution(NewKey())));

            var ticket = await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId);
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(20307, refusal.Code);
            Assert.Equal(422, refusal.HttpStatus);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(ticket.Exchanges);
            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
        }

        // ---------------------------------------------- document act recovery

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task G2D1_an_unresolved_document_refund_moves_no_value(ProviderOperationOutcome unresolved)
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);
            var key = NewKey();

            harness.DocumentRefunds.RefundOutcome = unresolved;
            harness.DocumentRefunds.RecoveryOutcome = unresolved;

            var held = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.NotNull(replay.SuccessorElectronicTicketId);
            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.NotEmpty(harness.DocumentRefunds.ObservedRecoveryKeys);
        }

        [Fact]
        public async Task G2D2_a_refused_document_refund_leaves_the_coupon_unrefunded_and_moves_no_value()
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);

            harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(
                ElectronicTicketStatus.Exchanged,
                (await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).StatusSummary);
            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.DoesNotContain(
                (await ReloadAsync(_fixture, scenario.OrderId)).Changes,
                change => change.ChangeType == OrderChangeType.Refund);
        }

        [Fact]
        public async Task G2D3_a_document_refund_the_caller_never_saw_is_recovered_and_never_repeated()
        {
            var caller = Caller();
            var refunds = new DeterministicDocumentRefundAdapter { ThrowAfterDispatch = true };

            await using var crashed = new OrderSliceHarness(_fixture, caller, documentRefunds: refunds);
            var scenario = await RefundScenarioAsync(crashed);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            refunds.ThrowAfterDispatch = false;
            refunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using var resumed = new OrderSliceHarness(_fixture, caller, documentRefunds: refunds);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(refunds.ObservedRefundRequests);
            Assert.Single(refunds.DispatchedKeys);
            Assert.Equal(EmdCouponStatus.Refunded, coupon.Status);
        }

        [Fact]
        public async Task G2D4_a_request_that_never_left_ordering_refunds_nothing_and_retries_once()
        {
            var caller = Caller();
            var refunds = new DeterministicDocumentRefundAdapter { ThrowBeforeDispatch = true };

            await using var crashed = new OrderSliceHarness(_fixture, caller, documentRefunds: refunds);
            var scenario = await RefundScenarioAsync(crashed);
            var key = NewKey();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashed.Exchange.ExchangeAsync(scenario.Execution(key)));

            Assert.Empty(refunds.DispatchedKeys);
            Assert.Equal(
                EmdCouponStatus.OpenForUse,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);

            refunds.ThrowBeforeDispatch = false;

            await using var resumed = new OrderSliceHarness(_fixture, caller, documentRefunds: refunds);
            Register(resumed, scenario);

            var finalized = await resumed.Exchange.ExchangeAsync(scenario.Execution(key));

            Assert.Equal(ServicingOperationStatus.Completed, finalized.OperationStatus);
            Assert.Single(refunds.DispatchedKeys);
            Assert.Equal(
                EmdCouponStatus.Refunded,
                (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single().Status);
        }

        [Theory]
        [InlineData("wrong-document")]
        [InlineData("wrong-coupon")]
        [InlineData("no-reference")]
        public async Task G2D5_a_contradictory_document_refund_confirmation_reconciles(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);

            switch (shape)
            {
                case "wrong-document":
                    harness.DocumentRefunds.ReportedDocumentNumber = NewDocumentNumber();
                    break;
                case "wrong-coupon":
                    harness.DocumentRefunds.ReportedCouponNumbers = [9];
                    break;
                default:
                    harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Confirmed;
                    harness.DocumentRefunds.ReportedDocumentNumber = null;
                    harness.DocumentRefunds.OmitProviderReference = true;
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
            Assert.Empty(harness.RefundValues.ObservedRequests);
        }

        // ---------------------------------------------- value act recovery

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task G2V1_an_unresolved_value_movement_keeps_the_coupon_refunded(
            ProviderOperationOutcome unresolved)
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);
            var key = NewKey();

            harness.RefundValues.RequestOutcome = unresolved;
            harness.RefundValues.RecoveryOutcome = unresolved;

            var held = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.OperationStatus);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Equal(EmdCouponStatus.Refunded, coupon.Status);
            Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.NotEmpty(harness.RefundValues.ObservedRecoveryKeys);
        }

        [Fact]
        public async Task G2V2_a_refused_value_movement_never_unrefunds_the_coupon()
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.NotNull(outcome.SuccessorElectronicTicketId);
            Assert.Equal(EmdCouponStatus.Refunded, coupon.Status);
            Assert.Equal(
                ElectronicTicketStatus.Exchanged,
                (await TicketAsync(_fixture, scenario.OrderId, scenario.TicketId)).StatusSummary);
        }

        [Theory]
        [InlineData("wrong-amount")]
        [InlineData("wrong-currency")]
        [InlineData("wrong-disposition")]
        public async Task G2V3_a_contradictory_value_movement_reconciles_without_unrefunding(string shape)
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);

            switch (shape)
            {
                case "wrong-amount":
                    harness.RefundValues.AmountOverride = RefundAmount + 1m;
                    break;
                case "wrong-currency":
                    harness.RefundValues.CurrencyOverride = 77;
                    break;
                default:
                    harness.RefundValues.DispositionOverride = "SomewhereElse";
                    break;
            }

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));
            var coupon = (await AncillaryAsync(_fixture, scenario.OrderId, _document)).Coupons.Single();

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(EmdCouponStatus.Refunded, coupon.Status);
            Assert.DoesNotContain(
                (await ReloadAsync(_fixture, scenario.OrderId)).Changes,
                change => change.ChangeType == OrderChangeType.Refund);
        }

        // ---------------------------------------------- frozen invariants

        [Fact]
        public async Task G2F1_a_completed_refund_replays_without_moving_a_document_or_any_money()
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);
            var key = NewKey();

            var first = await harness.Exchange.ExchangeAsync(scenario.Execution(key));
            var replay = await harness.Exchange.ExchangeAsync(scenario.Execution(key));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var after = await ReloadAsync(_fixture, scenario.OrderId);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.SuccessorElectronicTicketId, replay.SuccessorElectronicTicketId);
            Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Refund);
            Assert.Single(after.PriceChangeSets, set => set.Reason == PriceChangeReason.Refund);
            Assert.Single(
                ancillary.Coupons.Single().AssociationChanges,
                change => change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue);
            Assert.Single(
                await TicketsAsync(_fixture, scenario.OrderId),
                ticket => ticket.PredecessorElectronicTicketId == scenario.TicketId);
        }

        [Fact]
        public async Task G2F2_a_refunded_coupon_is_never_reassociated_or_refunded_again()
        {
            await using var harness = NewHarness();
            var scenario = await RefundScenarioAsync(harness);

            await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            var ancillary = await AncillaryAsync(_fixture, scenario.OrderId, _document);
            var coupon = ancillary.Coupons.Single();

            Assert.Equal(EmdCouponStatus.Refunded, coupon.Status);
            Assert.False(ancillary.PermitsReassociation(coupon.CouponNumber, 999L, 12345L));
            Assert.False(ancillary.PermitsRefund(coupon.CouponNumber, 999L));
            Assert.Empty(ancillary.CouponsAssociatedWith([coupon.AssociatedTicketCouponId ?? 0L]));
        }

        private async Task<ExchangeScenario> RefundScenarioAsync(OrderSliceHarness harness)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, harness, scenario.OrderId, _document, [scenario.CouponId]);
            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.Refund;

            return scenario;
        }

        private async Task<ElectronicTicket> SuccessorAsync(ExchangeScenario scenario, ExchangeOutcome outcome)
            => (await TicketsAsync(_fixture, scenario.OrderId))
                .Single(ticket => ticket.Id == outcome.SuccessorElectronicTicketId);

        private const decimal RefundAmount = 50_000m;

        private static string NewDocumentNumber() => $"M{Random.Shared.NextInt64(100_000_000, 999_999_999)}";

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7436, $"ancref-{Guid.NewGuid():N}");
    }
}
