using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Query.OrderAggregate.View;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingInventoryConsistencyTests
    {
        private const VoidReason Reason = VoidReason.AgentError;
        private const long Actor = 7;

        private readonly OrderingDatabaseFixture _fixture;

        public ServicingInventoryConsistencyTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task R15_Stale_inventory_never_rolls_back_confirmed_document_truth()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var before = (await TicketsAsync(order.Id))
                .Select(ticket => (ticket.Id, ticket.StatusSummary, ticket.DocumentVersion))
                .ToList();

            await DriftReservationAsync(order.Id, ReservationMemberStatus.Pending);

            await using var reading = NewHarness();
            var reservations = await ReservationEvidenceAsync(reading, order.Id);

            Assert.All(
                reservations,
                evidence =>
                {
                    Assert.Equal(ReservationMemberStatus.Pending, evidence.ObservedStatus);
                    Assert.True(evidence.IsObservationUnresolved);
                });

            var after = (await TicketsAsync(order.Id))
                .Select(ticket => (ticket.Id, ticket.StatusSummary, ticket.DocumentVersion))
                .ToList();

            Assert.Equal(before, after);
            Assert.All(after, ticket => Assert.Equal(ElectronicTicketStatus.Issued, ticket.StatusSummary));
        }

        [Fact]
        public async Task R16_An_inventory_release_repair_reuses_the_stable_operation_key()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);
            var dispatched = harness.Reservation.ObservedOperationKeys.ToList();

            var replay = await harness.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(dispatched, harness.Reservation.ObservedOperationKeys);
            Assert.NotEmpty(harness.Reservation.ObservedRecoveryKeys);
            Assert.Single(harness.Reservation.ObservedRecoveryKeys.Distinct());
            Assert.Contains(
                harness.Reservation.ObservedRecoveryKeys.Distinct().Single(),
                harness.Reservation.ObservedOperationKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task R17_An_unresolved_inventory_release_is_never_blindly_redispatched(
            ProviderOperationOutcome unresolved)
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = unresolved;
            harness.Reservation.RecoveryOutcome = unresolved;

            var first = await harness.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);

            var dispatched = harness.Reservation.ObservedOperationKeys.Count;

            var replay = await harness.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, replay.OperationStatus);
            Assert.Equal(dispatched, harness.Reservation.ObservedOperationKeys.Count);
            Assert.NotEmpty(harness.Reservation.ObservedRecoveryKeys);

            var reloaded = await ReloadAsync(order.Id);

            Assert.NotEqual(OrderStatus.Cancelled, reloaded.Status);
        }

        [Fact]
        public async Task R18_A_rejected_inventory_release_never_fabricates_local_success()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;

            var outcome = await harness.Cancel.CancelAsync(order.Id, Reason, Actor, NewKey(), null);

            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);

            var reloaded = await ReloadAsync(order.Id);

            Assert.NotEqual(OrderStatus.Cancelled, reloaded.Status);

            await using var reading = NewHarness();
            var view = (await reading.ReconciliationView.FindAsync(outcome.OperationId))!;

            Assert.True(view.IsRejected);
            Assert.False(view.IsUnresolved);
        }

        [Fact]
        public async Task R19_An_inventory_contradiction_produces_actionable_reconciliation_evidence()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);
            var key = NewKey();

            harness.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
            harness.Reservation.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);
            var reconciling = await harness.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, reconciling.OperationStatus);

            await using var reading = NewHarness();
            var view = (await reading.ReconciliationView.FindAsync(reconciling.OperationId))!;

            var evidence = Assert.Single(view.ExternalEvidence);

            Assert.Equal(ServicingEvidenceStage.ReservationRelease, evidence.Stage);
            Assert.Equal(ProviderOperationOutcome.Unknown, evidence.Outcome);
            Assert.True(evidence.IsUnresolved);

            Assert.Equal(ServicingOperationKind.Cancel, view.Kind);
            Assert.Equal(nameof(ServicingEvidenceStage.ReservationRelease), view.UnresolvedStage);
            Assert.Equal(ServicingRecoveryAction.ReplayCommand, view.RecoveryAction);
            Assert.NotEmpty(view.ReservationEvidence);
            Assert.All(
                view.ReservationEvidence,
                reservation =>
                {
                    Assert.NotEqual(0, reservation.FulfillmentReservationId);
                    Assert.NotEqual(0, reservation.OrderServiceId);
                    Assert.NotEqual(ReservationMemberStatus.Released, reservation.ObservedStatus);
                });
        }

        [Fact]
        public async Task R20_A_release_confirmed_before_a_failed_local_commit_is_adopted_once_without_a_second_release()
        {
            await using var setup = NewHarness();
            var order = await ReservedOrderAsync(setup);
            var caller = Caller();
            var key = NewKey();

            var operationId = await CrashAfterReleaseCheckpointAsync(caller, order.Id, key);

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

                var resumed = await resuming.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);

                Assert.Equal(operationId, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
                Assert.Empty(resuming.Reservation.ObservedOperationKeys);
                Assert.NotEmpty(resuming.Reservation.ObservedRecoveryKeys);
            }

            var cancelled = await ReloadAsync(order.Id);

            Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
            Assert.Single(cancelled.Changes, change => change.ChangeType == OrderChangeType.Cancel);

            await using (var replaying = new OrderSliceHarness(_fixture, caller))
            {
                var replay = await replaying.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);

                Assert.Equal(operationId, replay.OperationId);
                Assert.True(replay.IsReplay);
                Assert.Empty(replaying.Reservation.ObservedOperationKeys);
                Assert.Empty(replaying.Reservation.ObservedRecoveryKeys);
            }

            var replayed = await ReloadAsync(order.Id);

            Assert.Equal(cancelled.CommercialVersion, replayed.CommercialVersion);
            Assert.Single(replayed.Changes, change => change.ChangeType == OrderChangeType.Cancel);
            Assert.Equal(
                ProviderOperationOutcome.Confirmed,
                (await ServicingCrashWindow.EvidenceAsync(
                    _fixture, operationId, ServicingEvidenceStage.ReservationRelease)).Outcome);
        }

        [Fact]
        public async Task R21_A_recovered_rejection_never_overrides_a_durable_release_confirmation()
        {
            await using var setup = NewHarness();
            var order = await ReservedOrderAsync(setup);
            var caller = Caller();
            var key = NewKey();

            var operationId = await CrashAfterReleaseCheckpointAsync(caller, order.Id, key);

            await using var resuming = new OrderSliceHarness(_fixture, caller);

            resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Rejected;

            var resumed = await resuming.Cancel.CancelAsync(order.Id, Reason, Actor, key, null);

            Assert.Equal(operationId, resumed.OperationId);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, resumed.OperationStatus);
            Assert.Empty(resuming.Reservation.ObservedOperationKeys);
            Assert.NotEqual(OrderStatus.Cancelled, (await ReloadAsync(order.Id)).Status);
            Assert.Equal(
                ProviderOperationOutcome.Confirmed,
                (await ServicingCrashWindow.EvidenceAsync(
                    _fixture, operationId, ServicingEvidenceStage.ReservationRelease)).Outcome);
        }

        private async Task<long> CrashAfterReleaseCheckpointAsync(ICallerContext caller, long orderId, string key)
        {
            await using (var crashing = new OrderSliceHarness(_fixture, caller))
            {
                crashing.Events.FailCommit = true;

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => crashing.Cancel.CancelAsync(orderId, Reason, Actor, key, null));

                Assert.NotEmpty(crashing.Reservation.ObservedOperationKeys);
            }

            var crashed = await ServicingCrashWindow.OperationAsync(_fixture, orderId, ServicingOperationKind.Cancel);
            var durable = await ServicingCrashWindow.EvidenceAsync(
                _fixture, crashed.Id, ServicingEvidenceStage.ReservationRelease);

            Assert.Equal(ServicingOperationStatus.Prepared, crashed.Status);
            Assert.Equal(ProviderOperationOutcome.Confirmed, durable.Outcome);
            Assert.NotEqual(OrderStatus.Cancelled, (await ReloadAsync(orderId)).Status);

            await ServicingCrashWindow.ExpireRecoveryLeaseAsync(_fixture, orderId);

            return crashed.Id;
        }

        [Fact]
        public async Task R19b_An_agreeing_order_and_inventory_report_no_unresolved_work()
        {
            await using var harness = NewHarness();
            var order = await ReservedOrderAsync(harness);

            var outcome = await harness.Cancel.CancelAsync(order.Id, Reason, Actor, NewKey(), null);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            await using var reading = NewHarness();
            var view = (await reading.ReconciliationView.FindAsync(outcome.OperationId))!;

            Assert.False(view.IsUnresolved);
            Assert.Equal(ServicingRecoveryAction.NoneRequired, view.RecoveryAction);
            Assert.Empty(view.UnresolvedReservations);
            Assert.Empty(await reading.ReconciliationView.ListUnresolvedAsync(order.Id));
        }

        private static async Task<IReadOnlyList<ServicingReservationEvidence>> ReservationEvidenceAsync(
            OrderSliceHarness harness,
            long orderId)
        {
            var views = await harness.ReconciliationView.ListUnresolvedAsync(orderId);

            return views.Count > 0
                ? views[0].ReservationEvidence
                : [];
        }

        private async Task DriftReservationAsync(long orderId, ReservationMemberStatus observed)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                """
                UPDATE [Order].[FulfillmentReservationServices]
                SET [ObservedStatus] = {0}
                WHERE [FulfillmentReservationId] IN (
                    SELECT [Id] FROM [Order].[FulfillmentReservations] WHERE [OrderId] = {1})
                """,
                (int)observed,
                orderId);
        }

        private async Task<IReadOnlyList<Domain.ElectronicTicketAggregate.ElectronicTicket>> TicketsAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await new ElectronicTicketRepository(command).ListByOrderAsync(orderId);
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return (await new Persistence.OrderAggregate.OrderRepository(command).GetAsync(orderId))!;
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"inventory-{Guid.NewGuid():N}");

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static async Task<Order> ReservedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            return order;
        }

        private static async Task<Order> TicketedOrderAsync(OrderSliceHarness harness)
        {
            var order = await ReservedOrderAsync(harness);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            return order;
        }
    }
}
