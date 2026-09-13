using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class InvoluntaryBoundaryTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public InvoluntaryBoundaryTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task R25_A_voluntary_change_never_invents_an_involuntary_reason()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            var outcome = await RevalidationFixture.ChangeAsync(_fixture, harness, issued, 1);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            var order = await ReloadAsync(_fixture, issued.OrderId);

            Assert.DoesNotContain(
                order.Changes,
                change => change.ChangeType
                    is OrderChangeType.Reaccommodation or OrderChangeType.InvoluntaryChange);

            Assert.Single(order.Changes, change => change.ChangeType == OrderChangeType.VoluntaryChange);
        }

        [Fact]
        public async Task R25b_The_accepted_source_authority_reason_and_reference_are_preserved_verbatim()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);
            var before = await ReloadAsync(_fixture, issued.OrderId);

            await RevalidationFixture.ChangeAsync(_fixture, harness, issued, 1);

            var order = await ReloadAsync(_fixture, issued.OrderId);
            var change = order.Changes.Single(
                candidate => candidate.ChangeType == OrderChangeType.VoluntaryChange);

            Assert.Equal(PricingSource.PricingEngine, change.Source);
            Assert.Equal(RevalidationFixture.QuotedChangeId, change.Reason);
            Assert.Equal(RevalidationFixture.TargetSelectionRef, change.ExternalReference);
            Assert.NotNull(change.OperationId);
            Assert.Equal(before.CommercialVersion + 1, order.CommercialVersion);
        }

        [Fact]
        public async Task R26_A_source_selected_revalidate_routes_to_the_frozen_revalidation_rail()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            harness.DocumentChangeEligibilities.Outcome = DocumentChangeEligibilityOutcome.Revalidate;

            var outcome = await RevalidationFixture.ChangeAsync(_fixture, harness, issued, 1);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(ServicingOperationKind.Revalidate, outcome.TechnicalOperation);
            Assert.Equal(ChangeDocumentOutcome.Revalidated, outcome.DocumentOutcome);
            Assert.Single(harness.DocumentRevalidations.ObservedRequests);
            Assert.Single(ticket.Revalidations);
            Assert.Equal(issued.TicketId, ticket.Id);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(harness.ExchangeQuotes.ObservedQuoteRequests);
        }

        [Fact]
        public async Task R27_A_source_selected_reissue_never_revalidates_or_mutates_the_document()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);
            var before = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var orderBefore = await ReloadAsync(_fixture, issued.OrderId);

            harness.DocumentChangeEligibilities.Outcome = DocumentChangeEligibilityOutcome.ReissueRequired;

            var outcome = await RevalidationFixture.ChangeAsync(_fixture, harness, issued, 1);
            var after = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var orderAfter = await ReloadAsync(_fixture, issued.OrderId);

            Assert.Equal(ChangeDocumentOutcome.ReissueRequired, outcome.DocumentOutcome);
            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);

            Assert.Empty(harness.DocumentRevalidations.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(after.Revalidations);
            Assert.Equal(before.DocumentVersion, after.DocumentVersion);
            Assert.Equal(before.StatusSummary, after.StatusSummary);
            Assert.Equal(orderBefore.CommercialVersion, orderAfter.CommercialVersion);
            Assert.DoesNotContain(
                orderAfter.Changes,
                change => change.ChangeType == OrderChangeType.VoluntaryChange);
        }

        [Fact]
        public async Task R28_An_accepted_action_without_source_economics_moves_no_money()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);
            var before = await ReloadAsync(_fixture, issued.OrderId);

            var outcome = await RevalidationFixture.ChangeAsync(_fixture, harness, issued, 1);
            var after = await ReloadAsync(_fixture, issued.OrderId);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            Assert.Equal(before.PricingLines.Count, after.PricingLines.Count);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.ObligationVersion, after.ObligationVersion);
            Assert.Equal(before.PriceChangeSets.Count, after.PriceChangeSets.Count);

            Assert.Empty(harness.ExchangeFunding.ObservedCaptures);
            Assert.Empty(harness.RefundValues.ObservedRequests);
            Assert.Empty(harness.ExchangeResiduals.ObservedRequests);
        }

        [Fact]
        public async Task R29_Insufficient_action_evidence_fails_closed_without_inference()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);
            var before = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var orderBefore = await ReloadAsync(_fixture, issued.OrderId);

            harness.DocumentChangeEligibilities.Outcome = DocumentChangeEligibilityOutcome.PendingEvidence;

            var outcome = await RevalidationFixture.ChangeAsync(_fixture, harness, issued, 1);
            var after = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var orderAfter = await ReloadAsync(_fixture, issued.OrderId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
            Assert.Equal(ChangeDocumentOutcome.Pending, outcome.DocumentOutcome);

            Assert.Empty(harness.DocumentRevalidations.ObservedRequests);
            Assert.Empty(harness.DocumentExchanges.ObservedRequests);
            Assert.Empty(after.Revalidations);
            Assert.Equal(before.DocumentVersion, after.DocumentVersion);
            Assert.Equal(orderBefore.CommercialVersion, orderAfter.CommercialVersion);

            await using var reading = NewHarness();
            var snapshot = (await reading.Reconciliation.FindOperationAsync(outcome.OperationId))!;
            var view = await reading.ReconciliationView.ComposeAsync(snapshot, CancellationToken.None);

            Assert.True(view.AwaitsExternal);
            Assert.Equal(ServicingRecoveryAction.ReplayCommand, view.RecoveryAction);
        }

        [Fact]
        public async Task R29b_No_servicing_rail_produces_an_involuntary_change_type()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            await RevalidationFixture.ChangeAsync(_fixture, harness, issued, 1);

            await using var cancelling = NewHarness();

            await cancelling.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, VoidReason.AgentError, "boundary", 7, NewKey());

            var order = await ReloadAsync(_fixture, issued.OrderId);

            Assert.NotEmpty(order.Changes);
            Assert.All(
                order.Changes,
                change => Assert.False(
                    change.ChangeType
                        is OrderChangeType.Reaccommodation or OrderChangeType.InvoluntaryChange));

            Assert.All(
                order.PriceChangeSets,
                set => Assert.NotEqual(PriceChangeReason.InvoluntaryChange, set.Reason));
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"involuntary-{Guid.NewGuid():N}");
    }
}
