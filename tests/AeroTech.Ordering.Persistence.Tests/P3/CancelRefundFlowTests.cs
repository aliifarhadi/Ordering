using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class CancelRefundFlowTests
    {
        private const string SourceSystem = "AirPrice";
        private const string Disposition = "OriginalFormOfPayment";
        private const string Reason = "RefundIssuedInError";
        private const string ReasonDetail = "agent selected the wrong document";
        private const decimal RefundedFare = 900_000m;
        private const decimal Penalty = 100_000m;

        private readonly OrderingDatabaseFixture _fixture;

        public CancelRefundFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_full_unused_refund_is_cancelled()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");

            Approve(harness);

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await TicketAsync(order.Id, ticket.Id);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Confirmed, outcome.DocumentCorrectionOutcome);
            Assert.Equal(ElectronicTicketStatus.Issued, after.StatusSummary);
            Assert.All(after.Coupons, coupon =>
                Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));
            Assert.NotEqual(refund.OperationId, outcome.OperationId);
        }

        [Fact]
        public async Task One_partial_refund_record_is_cancelled_without_touching_another()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var coupons = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            await using var harness = NewHarness();

            var first = await RefundAsync(harness, order.Id, ticket, [coupons[0].Id], "RFND-A", 300_000m, 0m);
            var second = await RefundAsync(harness, order.Id, ticket, [coupons[1].Id], "RFND-B", 200_000m, 0m);

            Approve(harness);

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, first));

            var after = await TicketAsync(order.Id, ticket.Id);

            Assert.Equal(
                TicketCouponFinancialStatus.Open,
                after.Coupons.Single(coupon => coupon.Id == coupons[0].Id).FinancialStatus);
            Assert.Equal(
                TicketCouponFinancialStatus.Refunded,
                after.Coupons.Single(coupon => coupon.Id == coupons[1].Id).FinancialStatus);

            Assert.Equal(2, after.Refunds.Count);
            var correction = Assert.Single(after.RefundCorrections);
            Assert.Equal(first.Id, correction.DocumentRefundRecordId);
            Assert.Null(after.CorrectionForRefund(second.Id));
            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, after.StatusSummary);
            Assert.Equal(first.Id, outcome.RefundRecordId);
        }

        [Fact]
        public async Task The_original_refund_record_is_left_untouched()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");

            Approve(harness);

            await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await TicketAsync(order.Id, ticket.Id);
            var preserved = after.Refunds.Single(record => record.Id == refund.Id);

            Assert.Equal(refund.OperationId, preserved.OperationId);
            Assert.Equal(refund.ApprovedAmount, preserved.ApprovedAmount);
            Assert.Equal(refund.QuotedRefundId, preserved.QuotedRefundId);
            Assert.Equal(refund.PriceChangeSetId, preserved.PriceChangeSetId);
            Assert.Equal(ProviderOperationOutcome.Confirmed, preserved.ValueMovementStatus);
            Assert.Equal(refund.ValueMovementReference, preserved.ValueMovementReference);
            Assert.Equal(
                refund.Coupons.Select(coupon => coupon.TicketCouponId).OrderBy(id => id),
                preserved.Coupons.Select(coupon => coupon.TicketCouponId).OrderBy(id => id));
        }

        [Fact]
        public async Task A_used_coupon_is_untouched_and_the_summary_is_rederived()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var coupons = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            await MarkCouponUsedAsync(coupons[0].Id);

            await using var harness = NewHarness();

            var refund = await RefundAsync(harness, order.Id, ticket, [coupons[1].Id], "RFND-P", 300_000m, 0m);

            Assert.Equal(ElectronicTicketStatus.Refunded, (await TicketAsync(order.Id, ticket.Id)).StatusSummary);

            Approve(harness);

            await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await TicketAsync(order.Id, ticket.Id);

            Assert.Equal(
                TicketCouponFinancialStatus.Used,
                after.Coupons.Single(coupon => coupon.Id == coupons[0].Id).FinancialStatus);
            Assert.Equal(
                TicketCouponFinancialStatus.Open,
                after.Coupons.Single(coupon => coupon.Id == coupons[1].Id).FinancialStatus);
            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, after.StatusSummary);
            Assert.Equal(ticket.DocumentVersion + 2, after.DocumentVersion);
        }

        [Fact]
        public async Task The_order_service_document_link_is_restored()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var coupon = ticket.Coupons.First();

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");

            var refundedService = (await ReloadAsync(order.Id)).OrderServices
                .Single(service => service.Id == coupon.CurrentOrderServiceId);

            Assert.Equal(OrderServiceDocumentStatus.Refunded, refundedService.DocumentStatus);
            Assert.Null(refundedService.ElectronicTicketId);

            Approve(harness);

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await ReloadAsync(order.Id);
            var restored = after.OrderServices.Single(service => service.Id == coupon.CurrentOrderServiceId);

            Assert.Equal(OrderServiceDocumentStatus.Issued, restored.DocumentStatus);
            Assert.Equal(OrderServiceFinancialStatus.Priced, restored.FinancialStatus);
            Assert.Equal(ticket.Id, restored.ElectronicTicketId);
            Assert.Equal(coupon.Id, restored.TicketCouponId);
            Assert.Contains(coupon.CurrentOrderServiceId, outcome.RestoredOrderServiceIds);
        }

        [Fact]
        public async Task The_correction_pricing_exactly_negates_the_refund_price_change_set()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var beforeRefund = await ReloadAsync(order.Id);
            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");

            Approve(harness);

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await ReloadAsync(order.Id);
            var refundLines = Lines(after, refund.PriceChangeSetId!.Value);
            var correctionLines = Lines(after, outcome.PriceChangeSetId!.Value);

            Assert.Equal(refundLines.Count, correctionLines.Count);

            foreach (var refundLine in refundLines)
            {
                var correction = Assert.Single(
                    correctionLines, line => line.OriginalPricingLineId == refundLine.Id);

                Assert.Equal(refundLine.ComponentType, correction.ComponentType);
                Assert.Equal(refundLine.Effect, correction.Effect);
                Assert.Equal(refundLine.SaleAmount, correction.SaleAmount);
                Assert.Equal(refundLine.SaleCurrencyId, correction.SaleCurrencyId);
                Assert.Equal(refundLine.OriginalAmount, correction.OriginalAmount);
                Assert.Equal(refundLine.OriginalCurrencyId, correction.OriginalCurrencyId);
                Assert.Equal(refundLine.ExchangeRate, correction.ExchangeRate);
                Assert.NotEqual(refundLine.Direction, correction.Direction);
                Assert.Equal(PricingLineRole.Adjustment, correction.LineRole);
                Assert.Equal(refund.OperationId, correction.RelatedOperationId);
            }

            var refundNet = refundLines.Where(line => line.AffectsCustomerBalance).Sum(line => line.SignedSaleAmount);
            var correctionNet = correctionLines.Where(line => line.AffectsCustomerBalance).Sum(line => line.SignedSaleAmount);

            Assert.Equal(-refundNet, correctionNet);
            Assert.Equal(beforeRefund.CustomerTotal, after.CustomerTotal);
        }

        [Fact]
        public async Task A_refund_reversal_line_is_corrected_by_an_adjustment()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var reversible = (await ReloadAsync(order.Id)).PricingLines
                .First(line => line.AffectsCustomerBalance
                               && line.LineRole != PricingLineRole.Reversal
                               && ticket.CarriedPricingLineIds().Contains(line.Id));

            var refund = await RefundAsync(
                harness,
                order.Id,
                ticket,
                ticket.RefundableCouponIds().ToList(),
                "RFND-REV",
                lines: [ReversalOf(reversible)],
                approvedRefundAmount: reversible.SaleAmount);

            Approve(harness);

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await ReloadAsync(order.Id);
            var refundLine = Assert.Single(Lines(after, refund.PriceChangeSetId!.Value));
            var correction = Assert.Single(Lines(after, outcome.PriceChangeSetId!.Value));

            Assert.Equal(PricingLineRole.Reversal, refundLine.LineRole);
            Assert.Equal(PricingLineRole.Adjustment, correction.LineRole);
            Assert.Equal(refundLine.Id, correction.OriginalPricingLineId);
            Assert.NotEqual(refundLine.Direction, correction.Direction);
        }

        [Fact]
        public async Task The_correction_is_ordering_derived_and_appends_one_cancel_refund_change()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var beforeCorrection = await ReloadAsync(order.Id);
            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");
            var afterRefund = await ReloadAsync(order.Id);

            Approve(harness);

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await ReloadAsync(order.Id);
            var change = Assert.Single(after.Changes, candidate => candidate.ChangeType == OrderChangeType.CancelRefund);
            var changeSet = Assert.Single(after.PriceChangeSets, set => set.ChangeId == change.Id);

            Assert.Equal(outcome.OrderChangeId, change.Id);
            Assert.Equal(PricingSource.OrderingDerived, changeSet.Source);
            Assert.Equal(PriceChangeReason.Correction, changeSet.Reason);
            Assert.Equal(afterRefund.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(afterRefund.FinancialSequence + 1, after.FinancialSequence);
            Assert.True(after.ObligationVersion > afterRefund.ObligationVersion);
            Assert.Equal(beforeCorrection.CustomerTotal, after.CustomerTotal);
            Assert.Equal(14, (int)OrderChangeType.CancelRefund);
        }

        [Fact]
        public async Task A_rejected_document_correction_changes_nothing()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");
            var before = await ReloadAsync(order.Id);

            Approve(harness);
            harness.DocumentRefundCorrections.CorrectionOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await ReloadAsync(order.Id);
            var untouched = await TicketAsync(order.Id, ticket.Id);

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Empty(untouched.RefundCorrections);
            Assert.Equal(ElectronicTicketStatus.Refunded, untouched.StatusSummary);
            Assert.All(untouched.Coupons, coupon =>
                Assert.Equal(TicketCouponFinancialStatus.Refunded, coupon.FinancialStatus));
            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.CancelRefund);
            Assert.Empty(harness.RefundValueCorrections.ObservedRequests);
        }

        [Fact]
        public async Task An_uncertain_document_correction_recovers_under_the_same_key()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            Approve(harness);
            harness.DocumentRefundCorrections.CorrectionOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.CancelRefund.CancelRefundAsync(
                Execution(order.Id, ticket, refund, key, before.CommercialVersion));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Empty((await TicketAsync(order.Id, ticket.Id)).RefundCorrections);

            harness.DocumentRefundCorrections.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.CancelRefund.CancelRefundAsync(
                Execution(order.Id, ticket, refund, key, before.CommercialVersion));

            var after = await TicketAsync(order.Id, ticket.Id);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Single(after.RefundCorrections);
            Assert.Single(harness.DocumentRefundCorrections.ObservedRecoveryKeys);
            Assert.Equal(
                harness.DocumentRefundCorrections.ObservedCorrectionKeys.Single(),
                harness.DocumentRefundCorrections.ObservedRecoveryKeys.Single());
            Assert.Equal(ElectronicTicketStatus.Issued, after.StatusSummary);
        }

        [Fact]
        public async Task An_unsettled_value_correction_leaves_the_document_and_pricing_corrected()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");
            var afterRefund = await ReloadAsync(order.Id);

            Approve(harness);
            harness.RefundValueCorrections.ThrowOnRequest = true;

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await ReloadAsync(order.Id);
            var corrected = await TicketAsync(order.Id, ticket.Id);
            var correction = Assert.Single(corrected.RefundCorrections);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Unknown, correction.ValueCorrectionStatus);
            Assert.Null(correction.ValueCorrectionReference);
            Assert.NotNull(correction.ValueCorrectionDetail);

            Assert.Equal(ElectronicTicketStatus.Issued, corrected.StatusSummary);
            Assert.Equal(afterRefund.CommercialVersion + 1, after.CommercialVersion);
            Assert.NotNull(correction.PriceChangeSetId);
        }

        [Fact]
        public async Task A_refund_whose_value_never_settled_cannot_be_cancelled()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            harness.RefundValues.RequestOutcome = ProviderOperationOutcome.Unknown;

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");

            Assert.Equal(ProviderOperationOutcome.Unknown, refund.ValueMovementStatus);

            Approve(harness);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund)));

            Assert.Equal(20242, refusal.Code);
            Assert.Empty(harness.DocumentRefundCorrections.ObservedEligibilityKeys);
            Assert.Empty(harness.DocumentRefundCorrections.ObservedCorrectionKeys);
            Assert.Empty((await TicketAsync(order.Id, ticket.Id)).RefundCorrections);
        }

        [Fact]
        public async Task A_completed_correction_replays_without_duplicates()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            Approve(harness);

            var first = await harness.CancelRefund.CancelRefundAsync(
                Execution(order.Id, ticket, refund, key, before.CommercialVersion));

            var settled = await ReloadAsync(order.Id);
            var correctionCalls = harness.DocumentRefundCorrections.ObservedCorrectionKeys.Count;
            var valueCalls = harness.RefundValueCorrections.ObservedRequests.Count;

            var replay = await harness.CancelRefund.CancelRefundAsync(
                Execution(order.Id, ticket, refund, key, before.CommercialVersion));

            var after = await ReloadAsync(order.Id);
            var corrected = await TicketAsync(order.Id, ticket.Id);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Single(corrected.RefundCorrections);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.CancelRefund);
            Assert.Equal(settled.CommercialVersion, after.CommercialVersion);
            Assert.Equal(settled.FinancialSequence, after.FinancialSequence);
            Assert.Equal(correctionCalls, harness.DocumentRefundCorrections.ObservedCorrectionKeys.Count);
            Assert.Equal(valueCalls, harness.RefundValueCorrections.ObservedRequests.Count);
        }

        [Fact]
        public async Task An_unrelated_document_is_unaffected()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var tickets = (await TicketsAsync(order.Id)).OrderBy(candidate => candidate.Id).ToList();
            var ticket = tickets.First();
            var other = tickets.Last();

            Assert.NotEqual(ticket.Id, other.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");

            Approve(harness);

            await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var untouched = await TicketAsync(order.Id, other.Id);

            Assert.Equal(other.StatusSummary, untouched.StatusSummary);
            Assert.Equal(other.DocumentVersion, untouched.DocumentVersion);
            Assert.Empty(untouched.Refunds);
            Assert.Empty(untouched.RefundCorrections);
            Assert.All(untouched.Coupons, coupon =>
                Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));
        }

        [Fact]
        public async Task A_correction_without_affirmative_authorization_is_refused()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");
            var before = await ReloadAsync(order.Id);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund)));

            Assert.Equal(20244, refusal.Code);
            Assert.Equal(403, refusal.HttpStatus);
            Assert.Empty(harness.DocumentRefundCorrections.ObservedEligibilityKeys);
            Assert.Empty(harness.DocumentRefundCorrections.ObservedCorrectionKeys);
            Assert.Empty(harness.RefundValueCorrections.ObservedRequests);
            Assert.Empty((await TicketAsync(order.Id, ticket.Id)).RefundCorrections);
            Assert.Equal(before.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task A_refund_cannot_be_cancelled_twice()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");

            Approve(harness);

            await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var settled = await ReloadAsync(order.Id);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.CancelRefund.CancelRefundAsync(
                    Execution(order.Id, ticket, refund, NewKey(), settled.CommercialVersion)));

            Assert.Equal(20240, refusal.Code);
            Assert.Single((await TicketAsync(order.Id, ticket.Id)).RefundCorrections);
        }

        [Fact]
        public async Task The_correction_record_correlates_the_original_refund()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");

            Approve(harness);

            var outcome = await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var correction = Assert.Single((await TicketAsync(order.Id, ticket.Id)).RefundCorrections);

            Assert.Equal(outcome.OperationId, correction.OperationId);
            Assert.Equal(refund.Id, correction.DocumentRefundRecordId);
            Assert.Equal(refund.OperationId, correction.OriginalRefundOperationId);
            Assert.Equal(refund.ApprovedAmount, correction.CorrectedAmount);
            Assert.Equal(Reason, correction.Reason);
            Assert.Equal(ReasonDetail, correction.ReasonDetail);
            Assert.Equal(900L, correction.CorrectedBy);
            Assert.False(string.IsNullOrWhiteSpace(correction.ActorScope));
            Assert.Equal(outcome.PriceChangeSetId, correction.PriceChangeSetId);
            Assert.NotEqual(default, correction.CorrectedAt);
            Assert.Equal(ProviderOperationOutcome.Confirmed, correction.ValueCorrectionStatus);
            Assert.Equal(
                refund.Coupons.Select(coupon => coupon.TicketCouponId).OrderBy(id => id),
                correction.Coupons.Select(coupon => coupon.TicketCouponId).OrderBy(id => id));
        }

        [Fact]
        public async Task The_correction_touches_no_inventory_boundary()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refund = await RefundAsync(harness, order.Id, ticket, ticket.RefundableCouponIds().ToList(), "RFND-1");
            var reservationCalls = harness.Reservation.ObservedOperationKeys.Count;

            Approve(harness);

            await harness.CancelRefund.CancelRefundAsync(Execution(order.Id, ticket, refund));

            var after = await ReloadAsync(order.Id);

            Assert.Equal(reservationCalls, harness.Reservation.ObservedOperationKeys.Count);
            Assert.Empty(harness.Reservation.ObservedRecoveryKeys);
            Assert.DoesNotContain(after.OrderServices, service => service.Status == OrderServiceStatus.Cancelled);
        }

        private static void Approve(OrderSliceHarness harness)
            => harness.CancelRefundAuthorizations.Outcome = ManualRefundAuthorizationOutcome.Approved;

        private static CancelRefundExecution Execution(
            long orderId,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            string? key = null,
            int? expectedCommercialVersion = null)
            => new(
                orderId,
                ticket.Id,
                refund.Id,
                Reason,
                key ?? NewKey(),
                expectedCommercialVersion ?? CurrentVersion,
                ReasonDetail);

        private static int CurrentVersion { get; set; }

        private async Task<DocumentRefundRecord> RefundAsync(
            OrderSliceHarness harness,
            long orderId,
            ElectronicTicket ticket,
            IReadOnlyList<long> scope,
            string quotedRefundId,
            decimal refundedFare = RefundedFare,
            decimal penalty = Penalty,
            IReadOnlyList<AcceptedRefundPricingLine>? lines = null,
            decimal? approvedRefundAmount = null)
        {
            var current = await ReloadAsync(orderId);
            var pricing = lines ?? RefundLines(current, refundedFare, penalty);
            var amount = approvedRefundAmount ?? refundedFare - penalty;

            harness.RefundQuotes.Quote(
                QuoteOf(current, ticket, scope, pricing, amount) with { QuotedRefundId = quotedRefundId },
                AcceptedOf(current, ticket, scope, pricing, amount) with { QuotedRefundId = quotedRefundId });

            var outcome = await harness.Refund.RefundAsync(
                new RefundExecution(
                    orderId, ticket.Id, scope, NewKey(), current.CommercialVersion, quotedRefundId));

            CurrentVersion = outcome.CommercialVersion;

            return (await TicketAsync(orderId, ticket.Id)).Refunds
                .Single(record => record.OperationId == outcome.OperationId);
        }

        private static AcceptedRefundPricingLine ReversalOf(OrderPricingLine line)
            => new(
                line.ComponentType,
                line.Effect,
                line.Direction == OrderPricingLineDirection.Debit
                    ? OrderPricingLineDirection.Credit
                    : OrderPricingLineDirection.Debit,
                PricingLineRole.Reversal,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                line.BasisType,
                line.Refundability,
                ReversesPricingLineId: line.Id,
                ExchangeRate: line.ExchangeRate,
                Code: "RFND-REVERSAL");

        private static IReadOnlyList<OrderPricingLine> Lines(Order order, long priceChangeSetId)
            => order.PricingLines.Where(line => line.PriceChangeSetId == priceChangeSetId).ToList();

        private static RefundQuote QuoteOf(
            Order order,
            ElectronicTicket ticket,
            IReadOnlyList<long> scope,
            IReadOnlyList<AcceptedRefundPricingLine> lines,
            decimal approvedRefundAmount)
            => new(
                SourceSystem, "RFND", PricingSource.PricingEngine, order.Id, order.CommercialVersion,
                ticket.Id, order.CurrencyId, scope, lines, approvedRefundAmount, Disposition,
                DateTimeOffset.UtcNow.AddHours(1));

        private static AcceptedRefund AcceptedOf(
            Order order,
            ElectronicTicket ticket,
            IReadOnlyList<long> scope,
            IReadOnlyList<AcceptedRefundPricingLine> lines,
            decimal approvedRefundAmount)
            => new(
                SourceSystem, "RFND", PricingSource.PricingEngine, order.Id, order.CommercialVersion,
                ticket.Id, order.CurrencyId, scope, lines, approvedRefundAmount, Disposition,
                DateTimeOffset.UtcNow.AddHours(1), DispositionReference: "FOP-1");

        private static IReadOnlyList<AcceptedRefundPricingLine> RefundLines(
            Order order,
            decimal refundedFare,
            decimal penalty)
        {
            var lines = new List<AcceptedRefundPricingLine>
            {
                new(
                    PricingComponentType.Fare,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Credit,
                    PricingLineRole.Adjustment,
                    refundedFare,
                    order.CurrencyId,
                    refundedFare,
                    order.CurrencyId,
                    PricingBasisType.Order,
                    RefundabilityRule.Refundable,
                    Code: "RFND-FARE")
            };

            if (penalty > 0m)
                lines.Add(new AcceptedRefundPricingLine(
                    PricingComponentType.Penalty,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Debit,
                    PricingLineRole.Original,
                    penalty,
                    order.CurrencyId,
                    penalty,
                    order.CurrencyId,
                    PricingBasisType.Order,
                    RefundabilityRule.NonRefundable,
                    Code: "RFND-FEE"));

            return lines;
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"cxrfnd-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private async Task<Order> TicketedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var created = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            var order = await ReloadAsync(created.Id);

            CurrentVersion = order.CommercialVersion;

            return order;
        }

        private async Task MarkCouponUsedAsync(long couponId)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[TicketCoupons] SET [FinancialStatus] = {0} WHERE [Id] = {1}",
                (int)TicketCouponFinancialStatus.Used,
                couponId);
        }

        private async Task<ElectronicTicket> MultiCouponTicketAsync(long orderId)
        {
            var ticket = (await TicketsAsync(orderId)).FirstOrDefault(candidate => candidate.Coupons.Count > 1);

            Assert.NotNull(ticket);

            return ticket!;
        }

        private async Task<ElectronicTicket> FirstTicketAsync(long orderId)
            => (await TicketsAsync(orderId)).OrderBy(ticket => ticket.Id).First();

        private async Task<ElectronicTicket> TicketAsync(long orderId, long ticketId)
            => (await TicketsAsync(orderId)).Single(ticket => ticket.Id == ticketId);

        private async Task<IReadOnlyList<ElectronicTicket>> TicketsAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await new ElectronicTicketRepository(command).ListByOrderAsync(orderId);
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return (await new OrderRepository(context).GetAsync(orderId))!;
        }
    }
}
