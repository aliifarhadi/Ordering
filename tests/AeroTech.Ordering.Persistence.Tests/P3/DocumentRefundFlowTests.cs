using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
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
    public sealed class DocumentRefundFlowTests
    {
        private const string QuoteId = "RFND-QUOTE-1";
        private const string SourceSystem = "AirPrice";
        private const string Disposition = "OriginalFormOfPayment";
        private const decimal RefundedFare = 900_000m;
        private const decimal Penalty = 100_000m;

        private readonly OrderingDatabaseFixture _fixture;

        public DocumentRefundFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_completely_unused_electronic_ticket_is_refunded()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            Quote(harness, order, ticket);

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Confirmed, outcome.DocumentRefundOutcome);
            Assert.False(outcome.RefundNotAvailable);

            var refunded = await FirstTicketAsync(order.Id);

            Assert.Equal(ElectronicTicketStatus.Refunded, refunded.StatusSummary);
            Assert.All(refunded.Coupons, coupon =>
                Assert.Equal(TicketCouponFinancialStatus.Refunded, coupon.FinancialStatus));
            Assert.Equal(ticket.DocumentVersion + 1, refunded.DocumentVersion);
        }

        [Fact]
        public async Task A_refund_records_append_only_evidence_without_touching_issuance_provenance()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var issuanceOperationId = ticket.OperationId;
            var issuanceProviderReference = ticket.ProviderReference;

            Quote(harness, order, ticket);

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion);

            var refunded = await FirstTicketAsync(order.Id);
            var record = Assert.Single(refunded.Refunds);

            Assert.Equal(outcome.OperationId, record.OperationId);
            Assert.Equal(QuoteId, record.QuotedRefundId);
            Assert.Equal(RefundedFare - Penalty, record.ApprovedAmount);
            Assert.Equal(Disposition, record.ApprovedDisposition);
            Assert.Equal($"RFND-{refunded.DocumentNumber}", record.ProviderReference);
            Assert.Equal(outcome.PriceChangeSetId, record.PriceChangeSetId);
            Assert.NotEqual(default, record.RefundedAt);
            Assert.Equal(
                refunded.Coupons.Select(coupon => coupon.Id).OrderBy(id => id),
                record.Coupons.Select(coupon => coupon.TicketCouponId).OrderBy(id => id));

            Assert.Equal(issuanceOperationId, refunded.OperationId);
            Assert.Equal(issuanceProviderReference, refunded.ProviderReference);
            Assert.NotEqual(issuanceOperationId, record.OperationId);
        }

        [Fact]
        public async Task A_refund_commits_one_priced_commercial_change()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), before.CommercialVersion);

            var after = await ReloadAsync(order.Id);

            var change = Assert.Single(after.Changes, candidate => candidate.ChangeType == OrderChangeType.Refund);
            var changeSet = Assert.Single(after.PriceChangeSets, set => set.ChangeId == change.Id);

            Assert.Equal(outcome.OrderChangeId, change.Id);
            Assert.Equal(PriceChangeReason.Refund, changeSet.Reason);
            Assert.Equal(PricingSource.PricingEngine, changeSet.Source);
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence + 1, after.FinancialSequence);
            Assert.Equal(changeSet.FinancialSequence, after.FinancialSequence);
        }

        [Fact]
        public async Task A_refund_moves_the_customer_balance_and_advances_the_obligation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var before = await ReloadAsync(order.Id);
            var totalBefore = before.CustomerTotal;

            Quote(harness, order, ticket);

            await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, NewKey(), before.CommercialVersion);

            var after = await ReloadAsync(order.Id);

            Assert.Equal(totalBefore - RefundedFare + Penalty, after.CustomerTotal);
            Assert.True(after.ObligationVersion > before.ObligationVersion);
        }

        [Fact]
        public async Task A_refund_does_not_cancel_the_reservation_or_the_services()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var before = await ReloadAsync(order.Id);
            var reservedBefore = await ReservationStatusesAsync(order.Id);

            Quote(harness, order, ticket);

            await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, NewKey(), before.CommercialVersion);

            var after = await ReloadAsync(order.Id);

            Assert.NotEqual(CommercialSummary.Cancelled, after.CommercialSummary);
            Assert.DoesNotContain(after.OrderServices, service => service.Status == OrderServiceStatus.Cancelled);
            Assert.Equal(reservedBefore, await ReservationStatusesAsync(order.Id));
            Assert.DoesNotContain(harness.Reservation.ObservedOperationKeys, key => key.Contains("release:"));
        }

        [Fact]
        public async Task A_refunded_service_carries_refunded_document_and_financial_state()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            Quote(harness, order, ticket);

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion);

            var after = await ReloadAsync(order.Id);

            Assert.NotEmpty(outcome.RefundedOrderServiceIds);

            foreach (var service in after.OrderServices.Where(candidate =>
                         outcome.RefundedOrderServiceIds.Contains(candidate.Id)))
            {
                Assert.Equal(OrderServiceDocumentStatus.Refunded, service.DocumentStatus);
                Assert.Equal(OrderServiceFinancialStatus.Refunded, service.FinancialStatus);
                Assert.Null(service.ElectronicTicketId);
                Assert.Null(service.TicketCouponId);
            }
        }

        [Fact]
        public async Task A_refunded_service_leaves_the_issuance_obligation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var ticketCount = (await TicketsAsync(order.Id)).Count;

            Quote(harness, order, ticket);

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion);

            var after = await ReloadAsync(order.Id);
            var required = after.RequiredElectronicTicketServiceIds();

            Assert.NotEmpty(outcome.RefundedOrderServiceIds);
            Assert.DoesNotContain(required, serviceId => outcome.RefundedOrderServiceIds.Contains(serviceId));

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var reissued = await TicketsAsync(order.Id);

            Assert.Equal(ticketCount, reissued.Count);
            Assert.Equal(
                ElectronicTicketStatus.Refunded,
                reissued.Single(candidate => candidate.Id == ticket.Id).StatusSummary);
        }

        [Fact]
        public async Task An_order_whose_every_ticket_is_refunded_carries_no_issuance_obligation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);

            foreach (var ticket in (await TicketsAsync(order.Id)).OrderBy(candidate => candidate.Id).ToList())
            {
                var current = await ReloadAsync(order.Id);
                var quoteId = $"{QuoteId}-{ticket.Id}";

                harness.RefundQuotes.Quote(
                    QuoteOf(current, ticket) with { QuotedRefundId = quoteId },
                    AcceptedOf(current, ticket) with { QuotedRefundId = quoteId });

                await harness.Refund.RefundAsync(
                    order.Id, ticket.Id, quoteId, NewKey(), current.CommercialVersion);
            }

            var after = await ReloadAsync(order.Id);

            Assert.Empty(after.RequiredElectronicTicketServiceIds());
            Assert.Equal(OrderStatus.Refunded, after.Status);
            Assert.All(await TicketsAsync(order.Id), ticket =>
                Assert.Equal(ElectronicTicketStatus.Refunded, ticket.StatusSummary));
        }

        [Fact]
        public async Task A_replay_of_the_same_key_refunds_once()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var key = NewKey();

            Quote(harness, order, ticket);

            var first = await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, key, order.CommercialVersion);
            var settled = await ReloadAsync(order.Id);

            var replay = await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, key, order.CommercialVersion);
            var after = await ReloadAsync(order.Id);
            var refunded = await FirstTicketAsync(order.Id);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(first.OrderChangeId, replay.OrderChangeId);
            Assert.Single(refunded.Refunds);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Refund);
            Assert.Equal(settled.CommercialVersion, after.CommercialVersion);
            Assert.Equal(settled.FinancialSequence, after.FinancialSequence);
            Assert.Equal(settled.CustomerTotal, after.CustomerTotal);
        }

        [Fact]
        public async Task The_same_key_with_a_different_request_conflicts()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var key = NewKey();

            Quote(harness, order, ticket);

            await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, key, order.CommercialVersion);

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(order.Id, ticket.Id, "RFND-QUOTE-2", key, order.CommercialVersion));

            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
        }

        [Fact]
        public async Task A_rejected_document_refund_changes_nothing()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);
            harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), before.CommercialVersion);

            var after = await ReloadAsync(order.Id);
            var untouched = await FirstTicketAsync(order.Id);

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Equal(ElectronicTicketStatus.Issued, untouched.StatusSummary);
            Assert.Empty(untouched.Refunds);
            Assert.All(untouched.Coupons, coupon =>
                Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));
            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Refund);
        }

        [Fact]
        public async Task An_issuer_that_denies_the_refund_reports_it_before_any_mutation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            Quote(harness, order, ticket);
            harness.DocumentRefunds.Eligibility = EligibilityOutcome.Denied;

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion);

            Assert.True(outcome.RefundNotAvailable);
            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Equal(ElectronicTicketStatus.Issued, (await FirstTicketAsync(order.Id)).StatusSummary);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundKeys);
            Assert.Empty(harness.RefundQuotes.ObservedSelections);
        }

        [Fact]
        public async Task An_uncertain_document_refund_commits_nothing_and_recovers_once()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var key = NewKey();

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);
            harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Empty((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(before.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);

            harness.DocumentRefunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            var after = await ReloadAsync(order.Id);
            var refunded = await FirstTicketAsync(order.Id);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Equal(ElectronicTicketStatus.Refunded, refunded.StatusSummary);
            Assert.Single(refunded.Refunds);
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);

            var settled = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            Assert.True(settled.IsReplay);
            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(after.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task A_partially_used_ticket_is_refused_as_unsupported_not_as_non_refundable()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            await SetCouponFinancialStatusAsync(ticket.Id, TicketCouponFinancialStatus.Used);

            await using var refunding = NewHarness();

            var reloaded = await ReloadAsync(order.Id);

            Quote(refunding, reloaded, ticket);

            var unsupported = await Assert.ThrowsAsync<BusinessException>(
                () => refunding.Refund.RefundAsync(
                    order.Id, ticket.Id, QuoteId, NewKey(), reloaded.CommercialVersion));

            Assert.Equal(2918, unsupported.Code);
            Assert.Equal(422, unsupported.HttpStatus);
            Assert.Empty(refunding.DocumentRefunds.ObservedRefundKeys);
        }

        [Fact]
        public async Task An_already_refunded_ticket_is_refused_as_non_refundable()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            Quote(harness, order, ticket);

            await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion);

            await using var again = NewHarness();

            var reloaded = await ReloadAsync(order.Id);

            Quote(again, reloaded, ticket);

            var terminal = await Assert.ThrowsAsync<BusinessException>(
                () => again.Refund.RefundAsync(
                    order.Id, ticket.Id, QuoteId, NewKey(), reloaded.CommercialVersion));

            Assert.Equal(2917, terminal.Code);
            Assert.Equal(409, terminal.HttpStatus);
        }

        [Fact]
        public async Task A_coupon_under_external_control_is_refused_before_provider_execution()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            await SetCouponControlStatusAsync(ticket.Id, TicketCouponControlStatus.External);

            await using var refunding = NewHarness();

            var reloaded = await ReloadAsync(order.Id);

            Quote(refunding, reloaded, ticket);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => refunding.Refund.RefundAsync(
                    order.Id, ticket.Id, QuoteId, NewKey(), reloaded.CommercialVersion));

            Assert.Equal(2919, refusal.Code);
            Assert.Empty(refunding.DocumentRefunds.ObservedEligibilityKeys);
        }

        [Fact]
        public async Task An_accepted_refund_that_does_not_bind_to_the_request_is_refused()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            harness.RefundQuotes.Quote(
                QuoteOf(order, ticket),
                AcceptedOf(order, ticket) with { ExpectedCommercialVersion = order.CommercialVersion + 99 });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(
                    order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(2922, refusal.Code);
            Assert.Equal(ElectronicTicketStatus.Issued, (await FirstTicketAsync(order.Id)).StatusSummary);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundKeys);
        }

        [Fact]
        public async Task A_refund_priced_by_ordering_itself_is_refused()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            harness.RefundQuotes.Quote(
                QuoteOf(order, ticket),
                AcceptedOf(order, ticket) with { PricingSource = PricingSource.OrderingDerived });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(
                    order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(2926, refusal.Code);
            Assert.Equal(ElectronicTicketStatus.Issued, (await FirstTicketAsync(order.Id)).StatusSummary);
        }

        [Fact]
        public async Task An_unsettled_value_movement_does_not_undo_the_document_refund()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);
            harness.RefundValues.ThrowOnRequest = true;

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), before.CommercialVersion);

            var after = await ReloadAsync(order.Id);
            var refunded = await FirstTicketAsync(order.Id);
            var record = Assert.Single(refunded.Refunds);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(ProviderOperationOutcome.Unknown, outcome.ValueMovementOutcome);
            Assert.Equal(ProviderOperationOutcome.Unknown, record.ValueMovementStatus);
            Assert.Null(record.ValueMovementReference);
            Assert.NotNull(record.ValueMovementDetail);

            Assert.Equal(ElectronicTicketStatus.Refunded, refunded.StatusSummary);
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence + 1, after.FinancialSequence);
        }

        [Fact]
        public async Task A_settled_value_movement_is_recorded_against_the_refund()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            Quote(harness, order, ticket);

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion);

            var record = Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
            var request = Assert.Single(harness.RefundValues.ObservedRequests);

            Assert.Equal(ProviderOperationOutcome.Confirmed, outcome.ValueMovementOutcome);
            Assert.Equal(ProviderOperationOutcome.Confirmed, record.ValueMovementStatus);
            Assert.Equal($"VAL-{ticket.DocumentNumber}", record.ValueMovementReference);
            Assert.Equal(RefundedFare - Penalty, request.ApprovedAmount);
            Assert.Equal(Disposition, request.ApprovedDisposition);
            Assert.Contains(ticket.Id.ToString(), request.OperationKey);
        }

        [Fact]
        public async Task A_refund_quote_is_free_of_side_effects()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);

            var quote = await harness.Refund.QuoteAsync(order.Id, ticket.Id);

            var after = await ReloadAsync(order.Id);

            Assert.Equal(QuoteId, quote.QuotedRefundId);
            Assert.Equal(SourceSystem, quote.SourceSystem);
            Assert.Equal(PricingSource.PricingEngine, quote.PricingSource);
            Assert.Equal(RefundedFare - Penalty, quote.ApprovedRefundAmount);
            Assert.NotEmpty(quote.PricingLines);

            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(ElectronicTicketStatus.Issued, (await FirstTicketAsync(order.Id)).StatusSummary);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Refund);
        }

        [Fact]
        public async Task Malformed_accepted_pricing_fails_before_the_document_is_refunded()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var incoherent = PricingLines(order)
                .Select(line => line.Effect == PricingEffect.CustomerBalance
                                && line.Direction == OrderPricingLineDirection.Credit
                    ? line with { SaleCurrencyId = order.CurrencyId + 1 }
                    : line)
                .ToList();

            harness.RefundQuotes.Quote(
                QuoteOf(order, ticket),
                AcceptedOf(order, ticket) with { PricingLines = incoherent });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(
                    order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(2770, refusal.Code);

            await AssertNothingHappenedAsync(harness, order, ticket);
        }

        [Fact]
        public async Task A_reversal_of_another_documents_pricing_line_fails_before_the_document_is_refunded()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var tickets = (await TicketsAsync(order.Id)).OrderBy(candidate => candidate.Id).ToList();
            var ticket = tickets.First();
            var other = tickets.Last();

            Assert.NotEqual(ticket.Id, other.Id);

            var foreignPricingLineId = other.PriceLinks.Select(link => link.PricingLineId).First();

            Assert.DoesNotContain(foreignPricingLineId, ticket.CarriedPricingLineIds());

            var borrowed = PricingLines(order)
                .Select(line => line.Effect == PricingEffect.CustomerBalance
                                && line.Direction == OrderPricingLineDirection.Credit
                    ? line with
                    {
                        LineRole = PricingLineRole.Reversal,
                        ReversesPricingLineId = foreignPricingLineId
                    }
                    : line)
                .ToList();

            harness.RefundQuotes.Quote(
                QuoteOf(order, ticket),
                AcceptedOf(order, ticket) with { PricingLines = borrowed });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(
                    order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(2934, refusal.Code);

            await AssertNothingHappenedAsync(harness, order, ticket);
        }

        [Fact]
        public async Task An_approved_amount_that_contradicts_its_own_lines_fails_before_the_document_is_refunded()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            harness.RefundQuotes.Quote(
                QuoteOf(order, ticket),
                AcceptedOf(order, ticket) with { ApprovedRefundAmount = RefundedFare });

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(
                    order.Id, ticket.Id, QuoteId, NewKey(), order.CommercialVersion));

            Assert.Equal(2935, refusal.Code);

            await AssertNothingHappenedAsync(harness, order, ticket);
        }

        [Fact]
        public async Task A_penalty_and_credit_decomposition_reconciles_with_the_approved_amount()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);

            var outcome = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, NewKey(), before.CommercialVersion);

            var after = await ReloadAsync(order.Id);
            var lines = after.PricingLines.Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId).ToList();

            var netCustomerCredit = -lines
                .Where(line => line.AffectsCustomerBalance)
                .Sum(line => line.SignedSaleAmount);

            Assert.Equal(RefundedFare - Penalty, netCustomerCredit);
            Assert.Equal(netCustomerCredit, outcome.ApprovedRefundAmount);
            Assert.Equal(before.CustomerTotal - netCustomerCredit, after.CustomerTotal);
            Assert.Equal(netCustomerCredit, Assert.Single(harness.RefundValues.ObservedRequests).ApprovedAmount);

            Assert.Equal(
                Penalty,
                Assert.Single(lines, line => line.ComponentType == PricingComponentType.Penalty).SaleAmount);
        }

        [Fact]
        public async Task An_ambiguous_replay_recovers_the_value_movement_instead_of_requesting_it_again()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var key = NewKey();

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);
            harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Unknown;

            await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            harness.DocumentRefunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            harness.RefundValues.RecoveredAsDispatched = true;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            var refunded = await FirstTicketAsync(order.Id);
            var record = Assert.Single(refunded.Refunds);

            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Single(harness.RefundValues.ObservedRecoveryKeys);
            Assert.Equal(
                $"refund-value:{ticket.Id}:{recovered.OperationId}",
                harness.RefundValues.ObservedRecoveryKeys.Single());

            Assert.Equal(ProviderOperationOutcome.Confirmed, recovered.ValueMovementOutcome);
            Assert.Equal(ProviderOperationOutcome.Confirmed, record.ValueMovementStatus);
            Assert.Equal($"VAL-RECOVERED-{recovered.OperationId}", record.ValueMovementReference);
            Assert.Equal(ElectronicTicketStatus.Refunded, refunded.StatusSummary);
        }

        [Fact]
        public async Task An_unsettled_recovered_value_movement_leaves_the_document_and_pricing_committed()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var key = NewKey();

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);
            harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Unknown;

            await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            harness.DocumentRefunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            harness.RefundValues.RecoveredAsDispatched = true;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            var recovered = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            var after = await ReloadAsync(order.Id);
            var refunded = await FirstTicketAsync(order.Id);
            var record = Assert.Single(refunded.Refunds);

            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Equal(ProviderOperationOutcome.Unknown, record.ValueMovementStatus);
            Assert.Null(record.ValueMovementReference);

            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
            Assert.Equal(ElectronicTicketStatus.Refunded, refunded.StatusSummary);
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence + 1, after.FinancialSequence);
            Assert.Equal(record.Id, Assert.Single(refunded.Refunds).Id);
            Assert.NotNull(record.PriceChangeSetId);
        }

        [Fact]
        public async Task A_replay_that_never_dispatched_value_movement_may_still_request_it_once()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var key = NewKey();

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);
            harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Unknown;

            await harness.Refund.RefundAsync(order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            harness.DocumentRefunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            harness.RefundValues.RecoveredAsDispatched = false;

            var recovered = await harness.Refund.RefundAsync(
                order.Id, ticket.Id, QuoteId, key, before.CommercialVersion);

            var record = Assert.Single((await FirstTicketAsync(order.Id)).Refunds);

            Assert.Single(harness.RefundValues.ObservedRecoveryKeys);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovered.ValueMovementOutcome);
            Assert.Equal($"VAL-{ticket.DocumentNumber}", record.ValueMovementReference);
        }

        private async Task AssertNothingHappenedAsync(
            OrderSliceHarness harness,
            Order order,
            ElectronicTicket ticket)
        {
            var after = await ReloadAsync(order.Id);
            var untouched = (await TicketsAsync(order.Id)).Single(candidate => candidate.Id == ticket.Id);

            Assert.Single(harness.DocumentRefunds.ObservedEligibilityKeys);
            Assert.Single(harness.RefundQuotes.ObservedSelections);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundKeys);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.RefundValues.ObservedRecoveryKeys);

            Assert.Equal(ElectronicTicketStatus.Issued, untouched.StatusSummary);
            Assert.Empty(untouched.Refunds);
            Assert.All(untouched.Coupons, coupon =>
                Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));

            Assert.Equal(order.CommercialVersion, after.CommercialVersion);
            Assert.Equal(order.FinancialSequence, after.FinancialSequence);
            Assert.Equal(order.CustomerTotal, after.CustomerTotal);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Refund);
        }

        private static void Quote(OrderSliceHarness harness, Order order, ElectronicTicket ticket)
            => harness.RefundQuotes.Quote(QuoteOf(order, ticket), AcceptedOf(order, ticket));

        private static RefundQuote QuoteOf(Order order, ElectronicTicket ticket)
            => new(
                SourceSystem,
                QuoteId,
                PricingSource.PricingEngine,
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                order.CurrencyId,
                ticket.Coupons.Select(coupon => coupon.Id).ToList(),
                PricingLines(order),
                RefundedFare - Penalty,
                Disposition,
                DateTimeOffset.UtcNow.AddHours(1));

        private static AcceptedRefund AcceptedOf(Order order, ElectronicTicket ticket)
            => new(
                SourceSystem,
                QuoteId,
                PricingSource.PricingEngine,
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                order.CurrencyId,
                ticket.Coupons.Select(coupon => coupon.Id).ToList(),
                PricingLines(order),
                RefundedFare - Penalty,
                Disposition,
                DateTimeOffset.UtcNow.AddHours(1),
                SourcePricingReference: "AIRPRICE-REFUND-1",
                DispositionReference: "FOP-1");

        private static IReadOnlyList<AcceptedRefundPricingLine> PricingLines(Order order)
            =>
            [
                new(
                    PricingComponentType.Fare,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Credit,
                    PricingLineRole.Adjustment,
                    RefundedFare,
                    order.CurrencyId,
                    RefundedFare,
                    order.CurrencyId,
                    PricingBasisType.Order,
                    RefundabilityRule.Refundable,
                    Code: "RFND"),
                new(
                    PricingComponentType.Penalty,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Debit,
                    PricingLineRole.Original,
                    Penalty,
                    order.CurrencyId,
                    Penalty,
                    order.CurrencyId,
                    PricingBasisType.Order,
                    RefundabilityRule.NonRefundable,
                    Code: "RFNDFEE")
            ];

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private async Task<Order> TicketedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var created = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            return await ReloadAsync(created.Id);
        }

        private async Task SetCouponControlStatusAsync(long ticketId, TicketCouponControlStatus control)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[TicketCoupons] SET [ControlStatus] = {0} WHERE [TicketId] = {1}",
                (int)control,
                ticketId);
        }

        private async Task SetCouponFinancialStatusAsync(long ticketId, TicketCouponFinancialStatus financial)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[TicketCoupons] SET [FinancialStatus] = {0} WHERE [TicketId] = {1}",
                (int)financial,
                ticketId);
        }

        private async Task<IReadOnlyList<int>> ReservationStatusesAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await command.Database
                .SqlQueryRaw<int>(
                    "SELECT [Status] AS [Value] FROM [Order].[FulfillmentReservations] WHERE [OrderId] = {0} ORDER BY [Id]",
                    orderId)
                .ToListAsync();
        }

        private async Task<ElectronicTicket> FirstTicketAsync(long orderId)
            => (await TicketsAsync(orderId)).OrderBy(ticket => ticket.Id).First();

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
