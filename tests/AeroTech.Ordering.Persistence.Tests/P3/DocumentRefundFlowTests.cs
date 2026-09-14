using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion));

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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion));

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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), before.CommercialVersion));

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

            await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), before.CommercialVersion));

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

            await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), before.CommercialVersion));

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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion));

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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion));

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

                await harness.Refund.RefundAsync(Execution(order.Id, ticket, quoteId, NewKey(), current.CommercialVersion));
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

            var first = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));
            var settled = await ReloadAsync(order.Id);

            var replay = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));
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

            await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(Execution(order.Id, ticket, "RFND-QUOTE-2", key, order.CommercialVersion)));

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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), before.CommercialVersion));

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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion));

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

            var first = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Empty((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(before.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);

            harness.DocumentRefunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            var after = await ReloadAsync(order.Id);
            var refunded = await FirstTicketAsync(order.Id);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Equal(ElectronicTicketStatus.Refunded, refunded.StatusSummary);
            Assert.Single(refunded.Refunds);
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);

            var settled = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            Assert.True(settled.IsReplay);
            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(after.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task A_durably_confirmed_document_refund_is_adopted_without_asking_the_provider_again()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var key = NewKey();

            var before = await ReloadAsync(order.Id);

            Quote(harness, order, ticket);
            harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Empty((await FirstTicketAsync(order.Id)).Refunds);

            await harness.ServicingEvidence.RecordAsync(
                first.OperationId,
                ServicingEvidenceStage.DocumentRefund,
                ProviderOperationOutcome.Confirmed,
                "RFND-DURABLE",
                null,
                AccountableDocumentKind.ElectronicTicket,
                ticket.DocumentNumber);

            var dispatched = harness.DocumentRefunds.ObservedRefundKeys.Count;

            var adopted = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            var after = await ReloadAsync(order.Id);
            var refunded = await FirstTicketAsync(order.Id);

            Assert.Equal(first.OperationId, adopted.OperationId);
            Assert.Equal(ElectronicTicketStatus.Refunded, refunded.StatusSummary);
            Assert.Single(refunded.Refunds);
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(dispatched, harness.DocumentRefunds.ObservedRefundKeys.Count);
            Assert.Empty(harness.DocumentRefunds.ObservedRecoveryKeys);

            var replay = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            Assert.True(replay.IsReplay);
            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(after.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
            Assert.Empty(harness.DocumentRefunds.ObservedRecoveryKeys);
        }

        [Fact]
        public async Task A_refund_confirmed_before_a_failed_local_commit_is_adopted_once_by_the_same_operation()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await FirstTicketAsync(order.Id);
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            var documents = new DeterministicDocumentRefundAdapter();
            var values = new DeterministicRefundValueAdapter();
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                Quote(crashing, order, ticket);
                crashing.Events.FailCommit = true;

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => crashing.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion)));
            }

            var crashed = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Refund);
            var durable = await ServicingCrashWindow.EvidenceAsync(
                _fixture, crashed.Id, ServicingEvidenceStage.DocumentRefund);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Equal(ProviderOperationOutcome.Confirmed, durable.Outcome);
            Assert.Equal($"RFND-{ticket.DocumentNumber}", durable.ProviderReference);
            Assert.Empty((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(order.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
            Assert.Single(documents.ObservedRefundKeys);
            Assert.Single(values.ObservedRequests);

            await ServicingCrashWindow.ExpireRecoveryLeaseAsync(_fixture, order.Id);

            await using (var resuming = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                Quote(resuming, order, ticket);

                var resumed = await resuming.Refund.RefundAsync(
                    Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            }

            Assert.Single(documents.ObservedRefundKeys);
            Assert.Empty(documents.ObservedRecoveryKeys);
            Assert.Single(values.ObservedRequests);

            var refunded = await FirstTicketAsync(order.Id);
            var after = await ReloadAsync(order.Id);

            Assert.Equal(ElectronicTicketStatus.Refunded, refunded.StatusSummary);
            Assert.Single(refunded.Refunds);
            Assert.Equal(order.CommercialVersion + 1, after.CommercialVersion);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Refund);

            await using (var replaying = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                var replay = await replaying.Refund.RefundAsync(
                    Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                Assert.Equal(crashed.Id, replay.OperationId);
                Assert.True(replay.IsReplay);
            }

            var replayed = await ReloadAsync(order.Id);

            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(after.CommercialVersion, replayed.CommercialVersion);
            Assert.Single(replayed.Changes, change => change.ChangeType == OrderChangeType.Refund);
            Assert.Single(documents.ObservedRefundKeys);
            Assert.Empty(documents.ObservedRecoveryKeys);
            Assert.Single(values.ObservedRequests);
        }

        [Fact]
        public async Task A_refund_confirmation_contradicted_by_a_competing_durable_one_enters_reconciliation()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await FirstTicketAsync(order.Id);
            CompetingConfirmationEvidenceStore? competing = null;

            await using var harness = new OrderSliceHarness(
                _fixture,
                TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"),
                decorateEvidence: store => competing = new CompetingConfirmationEvidenceStore(store));

            Quote(harness, order, ticket);

            var outcome = await harness.Refund.RefundAsync(
                Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion));

            var durable = await ServicingCrashWindow.EvidenceAsync(
                _fixture, outcome.OperationId, ServicingEvidenceStage.DocumentRefund);
            var operation = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Refund);

            Assert.True(competing!.Competed);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, operation.Status);
            Assert.Equal(ProviderOperationOutcome.Confirmed, durable.Outcome);
            Assert.Equal(CompetingConfirmationEvidenceStore.CompetingReference, durable.ProviderReference);
            Assert.Equal(CompetingConfirmationEvidenceStore.CompetingDetail, durable.Detail);
            Assert.Empty((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(order.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
            Assert.Empty(harness.RefundValues.ObservedRequests);
        }

        [Fact]
        public async Task DA_a_crash_after_the_document_refund_reached_the_provider_recovers_first_and_never_refunds_twice()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await FirstTicketAsync(order.Id);
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            var documents = new DeterministicDocumentRefundAdapter { ThrowAfterDispatch = true };
            var values = new DeterministicRefundValueAdapter();
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents,
                             decorateEvidence: store => new UnreachableEvidenceStore(store)))
            {
                Quote(crashing, order, ticket);
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => crashing.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion)));
            }

            var crashed = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Refund);
            var dispatchedKey = Assert.Single(documents.ObservedRefundKeys);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Null(await ServicingCrashWindow.EvidenceOrNullAsync(
                _fixture, crashed.Id, ServicingEvidenceStage.DocumentRefund));
            Assert.True(await ServicingCrashWindow.ClaimIsBlockingAsync(_fixture, order.Id));
            Assert.Empty(values.ObservedRequests);

            await ServicingCrashWindow.ExpireRecoveryLeaseAsync(_fixture, order.Id);

            documents.ThrowAfterDispatch = false;

            await using (var resuming = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                Quote(resuming, order, ticket);

                var resumed = await resuming.Refund.RefundAsync(
                    Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            }

            Assert.Single(documents.ObservedRefundKeys);
            Assert.Equal(dispatchedKey, Assert.Single(documents.ObservedRecoveryKeys));
            Assert.Equal(
                ProviderOperationOutcome.Confirmed,
                (await ServicingCrashWindow.EvidenceAsync(_fixture, crashed.Id, ServicingEvidenceStage.DocumentRefund)).Outcome);

            var after = await ReloadAsync(order.Id);

            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(order.CommercialVersion + 1, after.CommercialVersion);

            await using (var replaying = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                var replay = await replaying.Refund.RefundAsync(
                    Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                Assert.True(replay.IsReplay);
            }

            Assert.Single(documents.ObservedRefundKeys);
            Assert.Single(documents.ObservedRecoveryKeys);
            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
            Assert.Equal(after.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task DA2_a_transport_failure_after_the_document_refund_reached_the_provider_keeps_the_order_claimed()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await FirstTicketAsync(order.Id);
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            var documents = new DeterministicDocumentRefundAdapter { ThrowAfterDispatch = true };
            var values = new DeterministicRefundValueAdapter();
            var key = NewKey();

            await using (var failing = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                Quote(failing, order, ticket);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => failing.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion)));
            }

            var held = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Refund);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.Status);
            Assert.Equal(
                ProviderOperationOutcome.Unknown,
                (await ServicingCrashWindow.EvidenceAsync(_fixture, held.Id, ServicingEvidenceStage.DocumentRefund)).Outcome);
            Assert.True(await ServicingCrashWindow.ClaimIsBlockingAsync(_fixture, order.Id));

            await using (var competing = NewHarness())
            {
                var refusal = await Assert.ThrowsAsync<BusinessException>(
                    () => competing.Cancel.CancelAsync(order.Id, VoidReason.AgentError, 7, NewKey(), null));

                Assert.Equal(ExchangeScenarios.ClaimConflict, refusal.Code);
            }

            documents.ThrowAfterDispatch = false;

            await using (var resuming = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                Quote(resuming, order, ticket);

                var resumed = await resuming.Refund.RefundAsync(
                    Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                Assert.Equal(held.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            }

            Assert.Single(documents.ObservedRefundKeys);
            Assert.Single(documents.ObservedRecoveryKeys);
            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
        }

        [Fact]
        public async Task DB_unknown_document_refund_evidence_survives_a_failed_local_save_and_is_recovered_first()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await FirstTicketAsync(order.Id);
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            var documents = new DeterministicDocumentRefundAdapter { RefundOutcome = ProviderOperationOutcome.Unknown };
            var values = new DeterministicRefundValueAdapter();
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                Quote(crashing, order, ticket);
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => crashing.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion)));
            }

            var crashed = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Refund);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Equal(
                ProviderOperationOutcome.Unknown,
                (await ServicingCrashWindow.EvidenceAsync(_fixture, crashed.Id, ServicingEvidenceStage.DocumentRefund)).Outcome);

            await ServicingCrashWindow.ExpireRecoveryLeaseAsync(_fixture, order.Id);

            documents.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using (var resuming = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                Quote(resuming, order, ticket);

                var resumed = await resuming.Refund.RefundAsync(
                    Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            }

            Assert.Single(documents.ObservedRefundKeys);
            Assert.Single(documents.ObservedRecoveryKeys);
            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
        }

        [Fact]
        public async Task DE_a_failed_execution_boundary_never_reaches_the_document_refund_provider()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await FirstTicketAsync(order.Id);
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            var documents = new DeterministicDocumentRefundAdapter();
            var values = new DeterministicRefundValueAdapter();
            var key = NewKey();
            RefusingExecutionBoundaryOperationStore? boundary = null;

            await using (var refusing = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents,
                             decorateOperationStore: store => boundary = new RefusingExecutionBoundaryOperationStore(store)))
            {
                Quote(refusing, order, ticket);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => refusing.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion)));
            }

            var refused = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Refund);

            Assert.Equal(1, boundary!.Refusals);
            Assert.Empty(documents.ObservedEligibilityKeys);
            Assert.Empty(documents.ObservedRefundKeys);
            Assert.Equal(ServicingOperationStatus.Prepared, refused.Status);

            await using (var retrying = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                Quote(retrying, order, ticket);

                var retried = await retrying.Refund.RefundAsync(
                    Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                Assert.Equal(refused.Id, retried.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, retried.OperationStatus);
            }

            Assert.Single(documents.ObservedRefundKeys);
        }

        [Fact]
        public async Task A_rejected_refund_replay_releases_the_replay_claim()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var key = NewKey();

            Quote(harness, order, ticket);
            harness.DocumentRefunds.RefundOutcome = ProviderOperationOutcome.Rejected;

            await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

            var replay = await harness.Refund.RefundAsync(
                Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

            Assert.True(replay.IsReplay);
            Assert.Equal(ServicingOperationStatus.Rejected, replay.OperationStatus);
            Assert.Single(harness.DocumentRefunds.ObservedRefundKeys);
            Assert.False(await ServicingCrashWindow.ClaimIsBlockingAsync(_fixture, order.Id));
        }

        [Fact]
        public async Task A_settled_coupon_is_refused_as_not_refundable()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            await SetCouponFinancialStatusAsync(ticket.Id, TicketCouponFinancialStatus.Used);

            await using var refunding = NewHarness();

            var reloaded = await ReloadAsync(order.Id);

            Quote(refunding, reloaded, ticket);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => refunding.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), reloaded.CommercialVersion)));

            Assert.Equal(20231, refusal.Code);
            Assert.Equal(409, refusal.HttpStatus);
            Assert.Empty(refunding.DocumentRefunds.ObservedRefundKeys);
        }

        [Fact]
        public async Task An_already_refunded_ticket_is_refused_as_non_refundable()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            Quote(harness, order, ticket);

            await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion));

            await using var again = NewHarness();

            var reloaded = await ReloadAsync(order.Id);

            Quote(again, reloaded, ticket);

            var terminal = await Assert.ThrowsAsync<BusinessException>(
                () => again.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), reloaded.CommercialVersion)));

            Assert.Equal(20211, terminal.Code);
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
                () => refunding.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), reloaded.CommercialVersion)));

            Assert.Equal(20213, refusal.Code);
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
                () => harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion)));

            Assert.Equal(20215, refusal.Code);
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
                () => harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion)));

            Assert.Equal(20219, refusal.Code);
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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), before.CommercialVersion));

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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion));

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

            var quote = await harness.Refund.QuoteAsync(order.Id, ticket.Id, ticket.RefundableCouponIds().ToList());

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
                () => harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion)));

            Assert.Equal(20110, refusal.Code);

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
                () => harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion)));

            Assert.Equal(20227, refusal.Code);

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
                () => harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), order.CommercialVersion)));

            Assert.Equal(20228, refusal.Code);

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

            var outcome = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, NewKey(), before.CommercialVersion));

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

            await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            harness.DocumentRefunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            harness.RefundValues.RecoveredAsDispatched = true;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

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

            await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            harness.DocumentRefunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            harness.RefundValues.RecoveredAsDispatched = true;
            harness.RefundValues.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            var recovered = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

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

            await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            harness.DocumentRefunds.RecoveryOutcome = ProviderOperationOutcome.Confirmed;
            harness.RefundValues.RecoveredAsDispatched = false;

            var recovered = await harness.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, before.CommercialVersion));

            var record = Assert.Single((await FirstTicketAsync(order.Id)).Refunds);

            Assert.Single(harness.RefundValues.ObservedRecoveryKeys);
            Assert.Single(harness.RefundValues.ObservedRequests);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovered.ValueMovementOutcome);
            Assert.Equal($"VAL-{ticket.DocumentNumber}", record.ValueMovementReference);
        }

        [Fact]
        public async Task DG_a_stale_snapshot_replay_of_a_completed_refund_never_refunds_twice()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await FirstTicketAsync(order.Id);
            var caller = TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}");
            var documents = new DeterministicDocumentRefundAdapter();
            var values = new DeterministicRefundValueAdapter();
            var key = NewKey();
            Order? refunded = null;
            var valueRequests = 0;
            CompletingElsewhereOrderRepository? interleaving = null;

            await using var stale = new OrderSliceHarness(
                _fixture, caller, refundValues: values, documentRefunds: documents,
                decorateOrders: orders => interleaving = new CompletingElsewhereOrderRepository(orders, async () =>
                {
                    await using var completing = new OrderSliceHarness(
                        _fixture, caller, refundValues: values, documentRefunds: documents);

                    Quote(completing, order, ticket);

                    var completed = await completing.Refund.RefundAsync(
                        Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                    Assert.Equal(ServicingOperationStatus.Completed, completed.OperationStatus);

                    refunded = await ReloadAsync(order.Id);
                    valueRequests = values.ObservedRequests.Count;
                }));

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => stale.Refund.RefundAsync(Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion)));

            Assert.Equal(1, interleaving!.Completions);
            Assert.Equal(20334, refusal.Code);
            Assert.Single(documents.ObservedRefundKeys);
            Assert.Single(documents.ObservedEligibilityKeys);
            Assert.Empty(documents.ObservedRecoveryKeys);
            Assert.Equal(valueRequests, values.ObservedRequests.Count);
            Assert.Empty(stale.RefundQuotes.ObservedSelections);

            var operation = await ServicingCrashWindow.OperationAsync(_fixture, order.Id, ServicingOperationKind.Refund);
            var after = await ReloadAsync(order.Id);

            Assert.Equal(ServicingOperationStatus.Completed, operation.Status);
            Assert.Equal(refunded!.CommercialVersion, after.CommercialVersion);
            Assert.Equal(refunded.FinancialSequence, after.FinancialSequence);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Refund);
            Assert.Single((await FirstTicketAsync(order.Id)).Refunds);
            Assert.False(await ServicingCrashWindow.ClaimIsBlockingAsync(_fixture, order.Id));

            await using (var retrying = new OrderSliceHarness(
                             _fixture, caller, refundValues: values, documentRefunds: documents))
            {
                var replay = await retrying.Refund.RefundAsync(
                    Execution(order.Id, ticket, QuoteId, key, order.CommercialVersion));

                Assert.True(replay.IsReplay);
                Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            }

            Assert.Single(documents.ObservedRefundKeys);
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

        private static RefundExecution Execution(
            long orderId,
            ElectronicTicket ticket,
            string quotedRefundId,
            string idempotencyKey,
            int? expectedCommercialVersion)
            => Execution(
                orderId,
                ticket,
                ticket.RefundableCouponIds().ToList(),
                quotedRefundId,
                idempotencyKey,
                expectedCommercialVersion);

        private static RefundExecution Execution(
            long orderId,
            ElectronicTicket ticket,
            IReadOnlyList<long> ticketCouponIds,
            string quotedRefundId,
            string idempotencyKey,
            int? expectedCommercialVersion)
            => new(
                orderId,
                ticket.Id,
                ticketCouponIds,
                idempotencyKey,
                expectedCommercialVersion,
                quotedRefundId);

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
