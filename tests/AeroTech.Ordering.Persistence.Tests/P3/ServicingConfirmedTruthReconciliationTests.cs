using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingConfirmedTruthReconciliationTests
    {
        private const VoidReason Reason = VoidReason.AgentError;
        private const string Detail = "confirmed truth";
        private const long Actor = 7;

        private readonly OrderingDatabaseFixture _fixture;

        public ServicingConfirmedTruthReconciliationTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task R35_A_used_coupon_stays_protected_and_reconciliation_never_mutates_it()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            await SetFinancialAsync(issued.TicketId, TicketCouponFinancialStatus.Used);

            var before = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            await using var voiding = NewHarness();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => voiding.VoidDocument.VoidAsync(
                    issued.OrderId, issued.TicketId, Reason, Detail, Actor, NewKey()));

            Assert.Equal(20204, error.Code);
            Assert.Empty(voiding.DocumentVoids.ObservedVoidKeys);

            await using var reading = NewHarness();

            await reading.ReconciliationView.ListUnresolvedAsync(issued.OrderId);

            var after = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(before.DocumentVersion, after.DocumentVersion);
            Assert.Equal(before.StatusSummary, after.StatusSummary);
            Assert.All(
                after.Coupons,
                coupon => Assert.Equal(TicketCouponFinancialStatus.Used, coupon.FinancialStatus));
        }

        [Fact]
        public async Task R36_A_confirmed_truth_is_never_downgraded_by_a_later_recovery()
        {
            var caller = Caller();

            await using var harness = NewHarness(caller);
            var issued = await IssuedAsync(_fixture, harness);
            var key = NewKey();

            var confirmed = await harness.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, Reason, Detail, Actor, key);

            Assert.Equal(ServicingOperationStatus.Completed, confirmed.OperationStatus);

            await using var replaying = NewHarness(caller);

            replaying.DocumentVoids.RecoveryOutcome = ProviderOperationOutcome.Rejected;

            var replay = await replaying.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, Reason, Detail, Actor, key);

            Assert.True(replay.IsReplay);
            Assert.Equal(confirmed.OperationId, replay.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
            Assert.Empty(replaying.DocumentVoids.ObservedVoidKeys);
            Assert.Empty(replaying.DocumentVoids.ObservedRecoveryKeys);

            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(ElectronicTicketStatus.Voided, ticket.StatusSummary);

            await using var reading = NewHarness();
            var view = (await reading.ReconciliationView.FindAsync(confirmed.OperationId))!;
            var evidence = Assert.Single(await reading.ServicingEvidence.ListAsync(confirmed.OperationId));

            Assert.Equal(ServicingOperationStatus.Completed, view.Status);
            Assert.Equal(ProviderOperationOutcome.Confirmed, evidence.Outcome);
            Assert.True(evidence.IsConfirmed);
        }

        [Fact]
        public async Task R37_A_confirmed_provider_mutation_is_never_redispatched()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);
            var key = NewKey();

            var confirmed = await harness.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, Reason, Detail, Actor, key);

            var dispatched = harness.DocumentVoids.ObservedVoidKeys.Count;

            await harness.VoidDocument.VoidAsync(issued.OrderId, issued.TicketId, Reason, Detail, Actor, key);
            await harness.VoidDocument.VoidAsync(issued.OrderId, issued.TicketId, Reason, Detail, Actor, key);

            Assert.Equal(dispatched, harness.DocumentVoids.ObservedVoidKeys.Count);
            Assert.Single(harness.DocumentVoids.ObservedVoidKeys.Distinct());
            Assert.Equal(
                ProviderOperationOutcome.Confirmed,
                (await harness.ServicingEvidence.ListAsync(confirmed.OperationId)).Single().Outcome);
        }

        [Fact]
        public async Task R38_A_replay_after_local_materialization_duplicates_no_local_truth()
        {
            var caller = Caller();

            await using var harness = NewHarness(caller);
            var issued = await IssuedAsync(_fixture, harness);
            var key = NewKey();

            await harness.VoidDocument.VoidAsync(issued.OrderId, issued.TicketId, Reason, Detail, Actor, key);

            var afterFirst = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var orderAfterFirst = await ReloadAsync(_fixture, issued.OrderId);

            await using var replaying = NewHarness(caller);

            await replaying.VoidDocument.VoidAsync(issued.OrderId, issued.TicketId, Reason, Detail, Actor, key);

            var afterReplay = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);
            var orderAfterReplay = await ReloadAsync(_fixture, issued.OrderId);

            Assert.Equal(afterFirst.DocumentVersion, afterReplay.DocumentVersion);
            Assert.Equal(afterFirst.StatusSummary, afterReplay.StatusSummary);
            Assert.Equal(afterFirst.PriceLinks.Count, afterReplay.PriceLinks.Count);

            Assert.Equal(orderAfterFirst.CommercialVersion, orderAfterReplay.CommercialVersion);
            Assert.Equal(orderAfterFirst.FinancialSequence, orderAfterReplay.FinancialSequence);
            Assert.Equal(orderAfterFirst.PricingLines.Count, orderAfterReplay.PricingLines.Count);
            Assert.Equal(orderAfterFirst.Changes.Count, orderAfterReplay.Changes.Count);
        }

        [Fact]
        public async Task R39_Parallel_workers_cannot_duplicate_the_same_mutation()
        {
            var caller = Caller();

            await using var setup = NewHarness(caller);
            var issued = await IssuedAsync(_fixture, setup);
            var key = NewKey();

            var versionBefore = (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).DocumentVersion;

            await using var first = NewHarness(caller);
            await using var second = NewHarness(caller);

            var results = await Task.WhenAll(
                AttemptAsync(first, issued, key),
                AttemptAsync(second, issued, key));

            var dispatched = first.DocumentVoids.ObservedVoidKeys.Count
                             + second.DocumentVoids.ObservedVoidKeys.Count;

            Assert.Contains(results, outcome => outcome is not null);
            Assert.Equal(1, dispatched);

            var operationIds = results
                .Where(outcome => outcome is not null)
                .Select(outcome => outcome!.OperationId)
                .Distinct()
                .ToList();

            Assert.Single(operationIds);

            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(ElectronicTicketStatus.Voided, ticket.StatusSummary);
            Assert.All(
                ticket.Coupons,
                coupon => Assert.Equal(TicketCouponFinancialStatus.Void, coupon.FinancialStatus));
            Assert.Equal(versionBefore + 1, ticket.DocumentVersion);

            await using var reading = NewHarness(caller);

            var evidence = await reading.ServicingEvidence.ListAsync(operationIds.Single());

            Assert.Single(evidence, entry => entry.Stage == ServicingEvidenceStage.DocumentVoid);

            var voidEvidence = evidence.Single(entry => entry.Stage == ServicingEvidenceStage.DocumentVoid);
            var outcomes = string.Join(",", results.Select(Describe));
            var rows = string.Join(",", evidence.Select(entry => entry.Stage + ":" + entry.Outcome));

            Assert.True(
                voidEvidence.Outcome == ProviderOperationOutcome.Confirmed,
                "outcome=" + voidEvidence.Outcome
                + "; detail=" + (voidEvidence.Detail ?? "-")
                + "; reference=" + (voidEvidence.ProviderReference ?? "-")
                + "; dispatched=" + dispatched
                + "; results=" + outcomes
                + "; evidence=" + rows);
        }

        [Fact]
        public async Task R40_A_completed_operation_retains_no_unresolved_obligation()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            var completed = await harness.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, Reason, Detail, Actor, NewKey());

            await using var reading = NewHarness();
            var view = (await reading.ReconciliationView.FindAsync(completed.OperationId))!;

            Assert.Equal(ServicingOperationStatus.Completed, view.Status);
            Assert.Null(view.UnresolvedStage);
            Assert.Empty(view.UnresolvedEvidence);
            Assert.Empty(view.ManualReviewReasons);
            Assert.Empty(view.ManualResolutions);
            Assert.Equal(ServicingRecoveryAction.NoneRequired, view.RecoveryAction);
            Assert.All(view.ExternalEvidence, evidence => Assert.True(evidence.IsConfirmed));
            Assert.Empty(await reading.ReconciliationView.ListUnresolvedAsync(issued.OrderId));
        }

        private static string Describe(
            Application.OrderAggregate.Services.DocumentVoid.DocumentVoidOutcome? outcome)
            => outcome is null ? "throw" : outcome.OperationStatus + "/" + outcome.IsReplay;

        private static async Task<Application.OrderAggregate.Services.DocumentVoid.DocumentVoidOutcome?>
            AttemptAsync(OrderSliceHarness harness, IssuedTicket issued, string key)
        {
            try
            {
                return await harness.VoidDocument.VoidAsync(
                    issued.OrderId, issued.TicketId, Reason, Detail, Actor, key);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private async Task SetFinancialAsync(long ticketId, TicketCouponFinancialStatus financial)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[TicketCoupons] SET [FinancialStatus] = {0} WHERE [TicketId] = {1}",
                (int)financial,
                ticketId);
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private OrderSliceHarness NewHarness(ICallerContext caller) => new(_fixture, caller);

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"truth-{Guid.NewGuid():N}");
    }
}
