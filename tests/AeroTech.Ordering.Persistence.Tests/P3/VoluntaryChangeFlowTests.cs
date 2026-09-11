using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class VoluntaryChangeFlowTests
    {
        private const string SourceSystem = "AirPrice";
        private const string QuoteId = "CHG-QUOTE-1";
        private const string TargetRef = "AIRPRICE-TARGET-1";

        private readonly OrderingDatabaseFixture _fixture;

        public VoluntaryChangeFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_change_quote_is_free_of_side_effects()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);

            var before = await ReloadAsync(order.Id);
            var ticketBefore = await TicketAsync(order.Id, target.TicketId);

            var quote = await harness.VoluntaryChange.QuoteAsync(order.Id, target.ServiceId);

            var after = await ReloadAsync(order.Id);
            var ticketAfter = await TicketAsync(order.Id, target.TicketId);

            Assert.Equal(QuoteId, quote.QuotedChangeId);
            Assert.Equal(ChangeMonetaryOutcome.Even, quote.MonetaryOutcome);
            Assert.Equal(TargetRef, quote.TargetSelectionRef);

            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.ObligationVersion, after.ObligationVersion);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.OrderServices.Count, after.OrderServices.Count);
            Assert.Equal(ticketBefore.DocumentVersion, ticketAfter.DocumentVersion);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.DocumentRevalidations.ObservedRequests);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.VoluntaryChange);
        }

        [Fact]
        public async Task A_successful_even_change_revalidates_the_same_ticket()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var ticketBefore = await TicketAsync(order.Id, target.TicketId);

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            var after = await ReloadAsync(order.Id);
            var ticketAfter = await TicketAsync(order.Id, target.TicketId);
            var coupon = ticketAfter.Coupons.Single(candidate => candidate.Id == target.CouponId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(OrderChangeType.VoluntaryChange, outcome.CommercialResult);
            Assert.Equal(ServicingOperationKind.Revalidate, outcome.TechnicalOperation);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, outcome.DocumentOutcome);

            Assert.Equal(ticketBefore.Id, ticketAfter.Id);
            Assert.Equal(ticketBefore.DocumentNumber, ticketAfter.DocumentNumber);
            Assert.Equal(ticketBefore.DocumentVersion + 1, ticketAfter.DocumentVersion);
            Assert.Equal(target.CouponId, coupon.Id);
            Assert.Equal(target.ServiceId, coupon.OrderServiceId);
            Assert.Equal(outcome.ReplacementOrderServiceId, coupon.CurrentOrderServiceId);
            Assert.NotEqual(target.ServiceId, coupon.CurrentOrderServiceId);

            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.VoluntaryChange);
            Assert.Equal(before.CommercialVersion + 1, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.ObligationVersion, after.ObligationVersion);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.PriceChangeSets.Count, after.PriceChangeSets.Count);
            Assert.Single(await TicketsAsync(order.Id), candidate => candidate.Id == ticketBefore.Id);
        }

        [Fact]
        public async Task The_replaced_service_is_superseded_without_refund_or_exchange_semantics()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var untouchedIds = before.OrderServices
                .Where(service => service.Id != target.ServiceId)
                .Select(service => service.Id)
                .ToList();

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            var after = await ReloadAsync(order.Id);
            var replaced = after.OrderServices.Single(service => service.Id == target.ServiceId);
            var replacement = after.OrderServices.Single(service => service.Id == outcome.ReplacementOrderServiceId);

            Assert.NotEqual(OrderServiceFinancialStatus.Refunded, replaced.FinancialStatus);
            Assert.NotEqual(OrderServiceDocumentStatus.Refunded, replaced.DocumentStatus);
            Assert.NotEqual(OrderServiceDocumentStatus.Voided, replaced.DocumentStatus);
            Assert.NotEqual(OrderServiceDocumentStatus.Exchanged, replaced.DocumentStatus);
            Assert.NotEqual(OrderServiceCommercialStatus.Exchanged, replaced.CommercialStatus);
            Assert.Null(replaced.ElectronicTicketId);

            Assert.NotEqual(target.ServiceId, replacement.Id);
            Assert.Equal(OrderServiceDocumentStatus.Issued, replacement.DocumentStatus);
            Assert.Equal(target.TicketId, replacement.ElectronicTicketId);
            Assert.Equal(target.CouponId, replacement.TicketCouponId);
            Assert.True(replacement.IsAirTransport);

            foreach (var untouched in untouchedIds)
                Assert.Contains(after.OrderServices, service => service.Id == untouched);
        }

        [Fact]
        public async Task An_accepted_plan_that_does_not_bind_fails_before_inventory()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order, acceptedVersionOffset: 99);
            var before = await ReloadAsync(order.Id);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before)));

            Assert.Equal(20251, refusal.Code);
            await AssertNothingHappenedAsync(harness, before, target);
        }

        [Fact]
        public async Task A_stale_expected_version_fails_before_inventory()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.VoluntaryChange.ChangeAsync(
                    new VoluntaryChangeExecution(
                        order.Id, target.ServiceId, QuoteId, NewKey(), before.CommercialVersion + 5)));

            Assert.Equal(20089, refusal.Code);
            await AssertNothingHappenedAsync(harness, before, target);
        }

        [Fact]
        public async Task A_monetary_change_outcome_is_deferred_before_inventory()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order, monetaryOutcome: ChangeMonetaryOutcome.AddCollect);
            var before = await ReloadAsync(order.Id);

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            Assert.True(outcome.DeferredToExchange);
            Assert.Equal(ChangeMonetaryOutcome.AddCollect, outcome.MonetaryOutcome);
            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            await AssertNothingHappenedAsync(harness, before, target);
        }

        [Theory]
        [InlineData(DocumentChangeEligibilityOutcome.ReissueRequired, ChangeDocumentOutcome.ReissueRequired)]
        [InlineData(DocumentChangeEligibilityOutcome.Denied, ChangeDocumentOutcome.Denied)]
        [InlineData(DocumentChangeEligibilityOutcome.PendingEvidence, ChangeDocumentOutcome.Pending)]
        public async Task A_non_revalidate_eligibility_stops_before_inventory(
            DocumentChangeEligibilityOutcome eligibility,
            ChangeDocumentOutcome expected)
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            harness.DocumentChangeEligibilities.Outcome = eligibility;

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            Assert.Equal(expected, outcome.DocumentOutcome);
            await AssertNothingHappenedAsync(harness, before, target);
        }

        [Fact]
        public async Task Inventory_receives_only_the_accepted_replacement_plan()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies);

            Assert.Equal(target.ServiceId, applied.Items.Single().ReplacedOrderServiceId);
            Assert.Equal(outcome.ReplacementOrderServiceId, applied.Items.Single().ReplacementOrderServiceId);
            Assert.Equal(ReplacementCapacityReference, applied.Items.Single().ReplacementFlightCapacityId);
            Assert.Equal(ReplacementBookingClass, applied.Items.Single().ReplacementBookingClass);
            Assert.Contains($"reservation-change:{target.ServiceId}", applied.OperationKey);
            Assert.Contains(outcome.OperationId.ToString(), applied.OperationKey);
        }

        [Fact]
        public async Task A_rejected_inventory_change_leaves_no_commercial_or_document_mutation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Empty(harness.DocumentRevalidations.ObservedRequests);

            var after = await ReloadAsync(order.Id);
            var ticket = await TicketAsync(order.Id, target.TicketId);

            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.OrderServices.Count, after.OrderServices.Count);
            Assert.Empty(ticket.Revalidations);
            Assert.Equal(target.ServiceId, ticket.Coupons.Single(c => c.Id == target.CouponId).CurrentOrderServiceId);
        }

        [Fact]
        public async Task An_uncertain_inventory_change_recovers_under_the_same_key()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(
                harness.ReservationChanges.ObservedApplies.Single().OperationKey,
                harness.ReservationChanges.ObservedRecoveryKeys.Single());
            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
            Assert.Equal(first.ReplacementOrderServiceId, recovered.ReplacementOrderServiceId);
        }

        [Fact]
        public async Task Inventory_confirmed_with_uncertain_document_recovers_without_a_second_apply()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.DocumentRevalidations.RevalidationOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(before.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);

            harness.DocumentRevalidations.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentRevalidations.ObservedRecoveryKeys);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
            Assert.Equal(first.ReplacementOrderServiceId, recovered.ReplacementOrderServiceId);
            Assert.Equal(before.CommercialVersion + 1, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task Inventory_confirmed_with_rejected_document_stays_reconcilable()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            harness.DocumentRevalidations.RevalidationOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ChangeDocumentOutcome.Rejected, outcome.DocumentOutcome);
            Assert.Single(harness.ReservationChanges.ObservedApplies);

            var after = await ReloadAsync(order.Id);
            var ticket = await TicketAsync(order.Id, target.TicketId);

            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Empty(ticket.Revalidations);
            Assert.Equal(before.OrderServices.Count, after.OrderServices.Count);
        }

        [Fact]
        public async Task A_replay_after_completion_produces_no_duplicates()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            var first = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var settled = await ReloadAsync(order.Id);

            var replay = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var after = await ReloadAsync(order.Id);
            var ticket = await TicketAsync(order.Id, target.TicketId);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(first.ReplacementOrderServiceId, replay.ReplacementOrderServiceId);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.VoluntaryChange);
            Assert.Single(ticket.Revalidations);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.DocumentRevalidations.ObservedRequests);
            Assert.Equal(settled.CommercialVersion, after.CommercialVersion);
            Assert.Equal(settled.OrderServices.Count, after.OrderServices.Count);
        }

        [Fact]
        public async Task The_durable_plan_keeps_the_replacement_identity_across_replay()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var plan = await harness.ChangePlans.FindAsync(first.OperationId);

            Assert.NotNull(plan);
            Assert.Equal(QuoteId, plan!.QuotedChangeId);
            Assert.Equal(TargetRef, plan.TargetSelectionRef);
            Assert.Equal(target.ServiceId, plan.ReplacedOrderServiceId);
            Assert.Equal(target.CouponId, plan.TicketCouponId);
            Assert.Equal(ChangeMonetaryOutcome.Even, plan.MonetaryOutcome);
            Assert.Equal(before.CommercialVersion, plan.ExpectedCommercialVersion);

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Equal(plan.ReplacementOrderServiceId, recovered.ReplacementOrderServiceId);
            Assert.Contains(
                (await ReloadAsync(order.Id)).OrderServices,
                service => service.Id == plan.ReplacementOrderServiceId);
        }

        [Fact]
        public async Task The_revalidation_record_explains_the_document_version_change()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            var ticket = await TicketAsync(order.Id, target.TicketId);
            var record = Assert.Single(ticket.Revalidations);

            Assert.Equal(outcome.OperationId, record.OperationId);
            Assert.Equal(QuoteId, record.QuotedChangeId);
            Assert.Equal(TargetRef, record.TargetSelectionRef);
            Assert.Equal(target.CouponId, record.TicketCouponId);
            Assert.Equal(target.ServiceId, record.PreviousOrderServiceId);
            Assert.Equal(outcome.ReplacementOrderServiceId, record.NewOrderServiceId);
            Assert.Equal(900L, record.RevalidatedBy);
            Assert.False(string.IsNullOrWhiteSpace(record.ActorScope));
            Assert.NotEqual(default, record.RevalidatedAt);
            Assert.NotNull(record.ProviderReference);

            Assert.NotNull(ticket.OperationId);
            Assert.Empty(ticket.Refunds);
            Assert.Null(ticket.VoidRecord);
        }

        [Fact]
        public async Task An_emd_association_to_the_same_coupon_survives_revalidation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            var documentsBefore = await harness.MiscDocumentRepository.ListByOrderAsync(order.Id);

            await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            var documentsAfter = await harness.MiscDocumentRepository.ListByOrderAsync(order.Id);
            var ticket = await TicketAsync(order.Id, target.TicketId);

            Assert.Equal(documentsBefore.Count, documentsAfter.Count);
            Assert.Equal(target.CouponId, ticket.Coupons.Single(c => c.Id == target.CouponId).Id);

            foreach (var document in documentsAfter)
            {
                var original = documentsBefore.Single(candidate => candidate.Id == document.Id);

                Assert.Equal(original.StatusSummary, document.StatusSummary);
                Assert.Equal(original.DocumentVersion, document.DocumentVersion);
            }
        }

        [Fact]
        public async Task Pending_evidence_persists_the_canonical_accepted_plan()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            harness.DocumentChangeEligibilities.Outcome = DocumentChangeEligibilityOutcome.PendingEvidence;

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            var plan = await harness.ChangePlans.FindAsync(outcome.OperationId);

            Assert.NotNull(plan);
            Assert.Equal(QuoteId, plan!.QuotedChangeId);
            Assert.Equal(TargetRef, plan.TargetSelectionRef);
            Assert.Equal(target.ServiceId, plan.ReplacedOrderServiceId);
            Assert.Equal(target.CouponId, plan.TicketCouponId);
            Assert.NotEqual(0, plan.ReplacementOrderServiceId);
            Assert.NotEqual(0, plan.ReplacementOrderSegmentId);
            Assert.Equal(DocumentChangeEligibilityOutcome.PendingEvidence, plan.EligibilityOutcome);
            Assert.False(plan.IsRevalidationEstablished);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
        }

        [Fact]
        public async Task A_pending_evidence_replay_reuses_the_plan_without_re_accepting()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.DocumentChangeEligibilities.Outcome = DocumentChangeEligibilityOutcome.PendingEvidence;

            var first = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var firstPlan = await harness.ChangePlans.FindAsync(first.OperationId);
            var acceptCalls = harness.ChangeQuotes.ObservedSelections.Count;

            var second = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var secondPlan = await harness.ChangePlans.FindAsync(second.OperationId);

            Assert.Equal(first.OperationId, second.OperationId);
            Assert.Equal(acceptCalls, harness.ChangeQuotes.ObservedSelections.Count);
            Assert.Equal(1, acceptCalls);

            Assert.Equal(firstPlan!.ReplacementOrderServiceId, secondPlan!.ReplacementOrderServiceId);
            Assert.Equal(firstPlan.ReplacementOrderSegmentId, secondPlan.ReplacementOrderSegmentId);

            Assert.Equal(2, harness.DocumentChangeEligibilities.ObservedRequests.Count);
            Assert.Equal(
                harness.DocumentChangeEligibilities.ObservedRequests[0].OperationKey,
                harness.DocumentChangeEligibilities.ObservedRequests[1].OperationKey);
            Assert.All(harness.DocumentChangeEligibilities.ObservedRequests, request =>
                Assert.Equal(TargetRef, request.TargetSelectionRef));

            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ChangeDocumentOutcome.Pending, second.DocumentOutcome);
        }

        [Fact]
        public async Task Pending_evidence_turning_revalidate_persists_eligibility_before_inventory()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.DocumentChangeEligibilities.Outcome = DocumentChangeEligibilityOutcome.PendingEvidence;

            var first = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var pendingPlan = await harness.ChangePlans.FindAsync(first.OperationId);

            harness.DocumentChangeEligibilities.Outcome = DocumentChangeEligibilityOutcome.Revalidate;

            var resumed = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var plan = await harness.ChangePlans.FindAsync(first.OperationId);
            var applied = Assert.Single(harness.ReservationChanges.ObservedApplies);

            Assert.Equal(DocumentChangeEligibilityOutcome.Revalidate, plan!.EligibilityOutcome);
            Assert.True(plan.IsRevalidationEstablished);

            Assert.Equal(pendingPlan!.ReplacementOrderServiceId, applied.Items.Single().ReplacementOrderServiceId);
            Assert.Equal(pendingPlan.ReplacementOrderSegmentId, applied.Items.Single().ReplacementOrderSegmentId);
            Assert.Equal(pendingPlan.ReplacementOrderServiceId, resumed.ReplacementOrderServiceId);

            Assert.Equal(1, harness.ChangeQuotes.ObservedSelections.Count);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, resumed.DocumentOutcome);
            Assert.Equal(before.CommercialVersion + 1, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task A_durable_revalidate_recovers_inventory_without_re_evaluating_eligibility()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Equal(
                DocumentChangeEligibilityOutcome.Revalidate,
                (await harness.ChangePlans.FindAsync(first.OperationId))!.EligibilityOutcome);

            var eligibilityCalls = harness.DocumentChangeEligibilities.ObservedRequests.Count;

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Equal(eligibilityCalls, harness.DocumentChangeEligibilities.ObservedRequests.Count);
            Assert.Equal(1, harness.ChangeQuotes.ObservedSelections.Count);
            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(first.ReplacementOrderServiceId, recovered.ReplacementOrderServiceId);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
        }

        [Theory]
        [InlineData(DocumentChangeEligibilityOutcome.ReissueRequired)]
        [InlineData(DocumentChangeEligibilityOutcome.Denied)]
        public async Task A_terminal_eligibility_persists_and_never_reaches_inventory(
            DocumentChangeEligibilityOutcome terminal)
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.DocumentChangeEligibilities.Outcome = terminal;

            var first = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var plan = await harness.ChangePlans.FindAsync(first.OperationId);

            Assert.Equal(terminal, plan!.EligibilityOutcome);
            Assert.True(plan.IsEligibilityTerminal);

            var replay = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Equal(first.DocumentOutcome, replay.DocumentOutcome);
            Assert.Equal(1, harness.ChangeQuotes.ObservedSelections.Count);
            Assert.Single(harness.DocumentChangeEligibilities.ObservedRequests);
            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);

            await AssertNothingHappenedAsync(harness, before, target);
        }

        [Fact]
        public async Task A_durable_revalidate_whose_reservation_was_never_dispatched_applies_once()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.ReservationChanges.ThrowBeforeDispatch = true;

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => harness.VoluntaryChange.ChangeAsync(
                    new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion)));

            var plan = await harness.ChangePlans.FindAsync(
                harness.ReservationChanges.ObservedApplies.Single().OperationId);

            Assert.Equal(DocumentChangeEligibilityOutcome.Revalidate, plan!.EligibilityOutcome);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, plan.ReservationOutcome);
            Assert.Empty(harness.ReservationChanges.DispatchedKeys);

            var attemptsBefore = harness.ReservationChanges.ObservedApplies.Count;
            var eligibilityCalls = harness.DocumentChangeEligibilities.ObservedRequests.Count;
            var acceptCalls = harness.ChangeQuotes.ObservedSelections.Count;

            harness.ReservationChanges.ThrowBeforeDispatch = false;

            var recovered = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var replayApply = Assert.Single(
                harness.ReservationChanges.ObservedApplies.Skip(attemptsBefore).ToList());

            Assert.Equal(attemptsBefore + 1, harness.ReservationChanges.ObservedApplies.Count);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Equal(replayApply.OperationKey, harness.ReservationChanges.ObservedRecoveryKeys.Single());

            Assert.Equal(plan.ReplacementOrderServiceId, replayApply.Items.Single().ReplacementOrderServiceId);
            Assert.Equal(plan.ReplacementOrderSegmentId, replayApply.Items.Single().ReplacementOrderSegmentId);
            Assert.Contains($"reservation-change:{target.ServiceId}", replayApply.OperationKey);

            Assert.Equal(eligibilityCalls, harness.DocumentChangeEligibilities.ObservedRequests.Count);
            Assert.Equal(acceptCalls, harness.ChangeQuotes.ObservedSelections.Count);
            Assert.Equal(1, acceptCalls);

            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
            Assert.Equal(plan.ReplacementOrderServiceId, recovered.ReplacementOrderServiceId);
        }

        [Fact]
        public async Task A_dispatched_reservation_reported_unknown_never_applies_again()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;
            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            var replay = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Single(harness.ReservationChanges.DispatchedKeys);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, replay.OperationStatus);
            Assert.Empty(harness.DocumentRevalidations.ObservedRequests);
            Assert.Equal(before.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task A_dispatched_reservation_reported_confirmed_never_applies_again()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Single(harness.DocumentRevalidations.ObservedRequests);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, recovered.DocumentOutcome);
        }

        [Fact]
        public async Task A_dispatched_reservation_reported_rejected_stays_distinct_from_never_dispatched()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);
            var key = NewKey();

            harness.ReservationChanges.ApplyOutcome = ProviderOperationOutcome.Unknown;

            await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            harness.ReservationChanges.RecoveryOutcome = ProviderOperationOutcome.Rejected;

            var rejected = await harness.VoluntaryChange.ChangeAsync(
                new VoluntaryChangeExecution(order.Id, target.ServiceId, QuoteId, key, before.CommercialVersion));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Single(harness.ReservationChanges.DispatchedKeys);
            Assert.Equal(ServicingOperationStatus.Rejected, rejected.OperationStatus);
            Assert.Empty(harness.DocumentRevalidations.ObservedRequests);

            var after = await ReloadAsync(order.Id);
            var ticket = await TicketAsync(order.Id, target.TicketId);

            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Empty(ticket.Revalidations);
            Assert.Equal(target.ServiceId, ticket.Coupons.Single(c => c.Id == target.CouponId).CurrentOrderServiceId);
        }

        [Fact]
        public async Task A_fresh_successful_change_applies_once_without_a_read_back()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var target = await ChangeTargetAsync(harness, order);
            var before = await ReloadAsync(order.Id);

            var outcome = await harness.VoluntaryChange.ChangeAsync(Execution(order.Id, target, before));

            Assert.Single(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Single(harness.ReservationChanges.DispatchedKeys);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, outcome.DocumentOutcome);
        }

        private const long ReplacementCapacityReference = 987_654L;
        private const string ReplacementBookingClass = "Q";

        private async Task AssertNothingHappenedAsync(
            OrderSliceHarness harness,
            Order before,
            ChangeTarget target)
        {
            var after = await ReloadAsync(before.Id);
            var ticket = await TicketAsync(before.Id, target.TicketId);

            Assert.Empty(harness.ReservationChanges.ObservedApplies);
            Assert.Empty(harness.ReservationChanges.ObservedRecoveryKeys);
            Assert.Empty(harness.DocumentRevalidations.ObservedRequests);
            Assert.Empty(ticket.Revalidations);

            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.ObligationVersion, after.ObligationVersion);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.OrderServices.Count, after.OrderServices.Count);
            Assert.Equal(target.ServiceId, ticket.Coupons.Single(c => c.Id == target.CouponId).CurrentOrderServiceId);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.VoluntaryChange);
        }

        private static VoluntaryChangeExecution Execution(long orderId, ChangeTarget target, Order before)
            => new(orderId, target.ServiceId, QuoteId, NewKey(), before.CommercialVersion);

        private sealed record ChangeTarget(long ServiceId, long TicketId, long CouponId);

        private async Task<ChangeTarget> ChangeTargetAsync(
            OrderSliceHarness harness,
            Order order,
            ChangeMonetaryOutcome monetaryOutcome = ChangeMonetaryOutcome.Even,
            int acceptedVersionOffset = 0)
        {
            var current = await ReloadAsync(order.Id);
            var tickets = await TicketsAsync(order.Id);
            var ticket = tickets.OrderBy(candidate => candidate.Id).First();
            var coupon = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).First();

            var quote = QuoteOf(current, ticket, coupon, monetaryOutcome, 0);
            var accepted = AcceptedOf(current, ticket, coupon, monetaryOutcome, acceptedVersionOffset);

            harness.ChangeQuotes.Quote(quote, accepted);

            return new ChangeTarget(coupon.CurrentOrderServiceId, ticket.Id, coupon.Id);
        }

        private static ChangeQuote QuoteOf(
            Order order,
            ElectronicTicket ticket,
            TicketCoupon coupon,
            ChangeMonetaryOutcome monetaryOutcome,
            int versionOffset)
            => new(
                SourceSystem, QuoteId, TargetRef, PricingSource.PricingEngine, order.Id,
                order.CommercialVersion + versionOffset, order.CurrencyId, ticket.Id,
                coupon.CurrentOrderServiceId, coupon.Id,
                order.OrderServices.Where(s => s.Id != coupon.CurrentOrderServiceId).Select(s => s.Id).ToList(),
                Replacement(order, coupon), monetaryOutcome, DateTimeOffset.UtcNow.AddHours(1));

        private static AcceptedVoluntaryChange AcceptedOf(
            Order order,
            ElectronicTicket ticket,
            TicketCoupon coupon,
            ChangeMonetaryOutcome monetaryOutcome,
            int versionOffset)
            => new(
                SourceSystem, QuoteId, TargetRef, PricingSource.PricingEngine, order.Id,
                order.CommercialVersion + versionOffset, order.CurrencyId, ticket.Id,
                coupon.CurrentOrderServiceId, coupon.Id,
                order.OrderServices.Where(s => s.Id != coupon.CurrentOrderServiceId).Select(s => s.Id).ToList(),
                Replacement(order, coupon), monetaryOutcome, DateTimeOffset.UtcNow.AddHours(1));

        private static AcceptedChangeReplacement Replacement(Order order, TicketCoupon coupon)
        {
            var service = order.OrderServices.Single(candidate => candidate.Id == coupon.CurrentOrderServiceId);
            var segment = order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value);

            return new AcceptedChangeReplacement(
                "REPLACEMENT-1",
                service.ServiceCode,
                service.Name,
                new AcceptedSegment(
                    "REPLACEMENT-SEG-1",
                    segment.Sequence,
                    segment.FlightId,
                    segment.FlightVersion,
                    segment.Number,
                    segment.OriginAirportId,
                    segment.OriginAirportTerminalId,
                    segment.DestinationAirportId,
                    segment.DestinationAirportTerminalId,
                    segment.MarketingAirlineId,
                    segment.OperatingAirlineId,
                    segment.DepartureDateTime.AddDays(2),
                    segment.ArrivalDateTime.AddDays(2),
                    segment.Duration,
                    segment.AircraftId,
                    segment.CabinClassId,
                    segment.RbdId,
                    ReplacementBookingClass,
                    segment.BookingClassCode,
                    ReplacementCapacityReference,
                    segment.AirFareId,
                    []),
                new AcceptedAirTransportDetail("REPLACEMENT-SEG-1"),
                service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToList());
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"chg-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private async Task<Order> TicketedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var created = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            return await ReloadAsync(created.Id);
        }

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
