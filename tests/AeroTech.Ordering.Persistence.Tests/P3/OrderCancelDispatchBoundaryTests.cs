using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ServicingCrashWindow;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OrderCancelDispatchBoundaryTests
    {
        private const ServicingEvidenceStage Stage = ServicingEvidenceStage.ReservationRelease;
        private const VoidReason Reason = VoidReason.AgentError;
        private const long Actor = 7;
        private const string ReleaseStep = "release";

        private readonly OrderingDatabaseFixture _fixture;

        public OrderCancelDispatchBoundaryTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task DA_a_crash_after_the_release_reached_the_provider_recovers_first_and_never_releases_twice()
        {
            var order = await ReservedOrderAsync();
            var caller = Caller();
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(
                             _fixture, caller,
                             decorateEvidence: store => new UnreachableEvidenceStore(store),
                             decorateReservationPort: port => new DispatchThenFailReservationPort(port)))
            {
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(() => CancelAsync(crashing, order.Id, key));

                Assert.NotEmpty(ReleaseKeys(crashing));
            }

            var crashed = await OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Null(await EvidenceOrNullAsync(_fixture, crashed.Id, Stage));
            Assert.True(await ClaimIsBlockingAsync(_fixture, order.Id));

            await ExpireRecoveryLeaseAsync(_fixture, order.Id);

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

                var resumed = await CancelAsync(resuming, order.Id, key);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
                Assert.Empty(ReleaseKeys(resuming));
                Assert.NotEmpty(resuming.Reservation.ObservedRecoveryKeys);
            }

            var cancelled = await ReloadAsync(order.Id);

            Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
            Assert.Single(cancelled.Changes, change => change.ChangeType == OrderChangeType.Cancel);
            Assert.Equal(ProviderOperationOutcome.Confirmed, (await EvidenceAsync(_fixture, crashed.Id, Stage)).Outcome);

            await using (var replaying = new OrderSliceHarness(_fixture, caller))
            {
                var replay = await CancelAsync(replaying, order.Id, key);

                Assert.True(replay.IsReplay);
                Assert.Empty(ReleaseKeys(replaying));
                Assert.Empty(replaying.Reservation.ObservedRecoveryKeys);
            }

            Assert.Equal(cancelled.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task DA2_a_transport_failure_after_the_release_reached_the_provider_keeps_the_order_claimed_until_recovered()
        {
            var order = await ReservedOrderAsync();
            var caller = Caller();
            var key = NewKey();

            await using (var failing = new OrderSliceHarness(
                             _fixture, caller, decorateReservationPort: port => new DispatchThenFailReservationPort(port)))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => CancelAsync(failing, order.Id, key));

                Assert.NotEmpty(ReleaseKeys(failing));
            }

            var held = await OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.Status);
            Assert.Equal(ProviderOperationOutcome.Unknown, (await EvidenceAsync(_fixture, held.Id, Stage)).Outcome);
            Assert.True(await ClaimIsBlockingAsync(_fixture, order.Id));

            await using (var competing = new OrderSliceHarness(_fixture, Caller()))
            {
                var refusal = await Assert.ThrowsAsync<BusinessException>(
                    () => CancelAsync(competing, order.Id, NewKey()));

                Assert.Equal(ExchangeScenarios.ClaimConflict, refusal.Code);
            }

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

                var resumed = await CancelAsync(resuming, order.Id, key);

                Assert.Equal(held.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
                Assert.Empty(ReleaseKeys(resuming));
                Assert.NotEmpty(resuming.Reservation.ObservedRecoveryKeys);
            }

            Assert.Equal(OrderStatus.Cancelled, (await ReloadAsync(order.Id)).Status);
        }

        [Fact]
        public async Task DB_unknown_release_evidence_survives_a_failed_local_save_and_is_recovered_before_any_second_release()
        {
            var order = await ReservedOrderAsync();
            var caller = Caller();
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(_fixture, caller))
            {
                crashing.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(() => CancelAsync(crashing, order.Id, key));
            }

            var crashed = await OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Equal(ProviderOperationOutcome.Unknown, (await EvidenceAsync(_fixture, crashed.Id, Stage)).Outcome);

            await ExpireRecoveryLeaseAsync(_fixture, order.Id);

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

                var resumed = await CancelAsync(resuming, order.Id, key);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
                Assert.Empty(ReleaseKeys(resuming));
                Assert.NotEmpty(resuming.Reservation.ObservedRecoveryKeys);
            }

            Assert.Equal(OrderStatus.Cancelled, (await ReloadAsync(order.Id)).Status);
        }

        [Fact]
        public async Task DD_a_rejected_release_whose_local_save_failed_is_recovered_and_never_released_again()
        {
            var order = await ReservedOrderAsync();
            var caller = Caller();
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(_fixture, caller))
            {
                crashing.Reservation.ReleaseOutcome = ProviderOperationOutcome.Rejected;
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(() => CancelAsync(crashing, order.Id, key));
            }

            var crashed = await OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);

            await ExpireRecoveryLeaseAsync(_fixture, order.Id);

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Rejected;

                var resumed = await CancelAsync(resuming, order.Id, key);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Rejected, resumed.OperationStatus);
                Assert.Empty(ReleaseKeys(resuming));
                Assert.NotEmpty(resuming.Reservation.ObservedRecoveryKeys);
            }

            Assert.NotEqual(OrderStatus.Cancelled, (await ReloadAsync(order.Id)).Status);
            Assert.False(await ClaimIsBlockingAsync(_fixture, order.Id));
        }

        [Fact]
        public async Task DE_a_failed_execution_boundary_never_reaches_the_release_provider()
        {
            var order = await ReservedOrderAsync();
            var caller = Caller();
            var key = NewKey();
            RefusingExecutionBoundaryOperationStore? boundary = null;

            await using (var refusing = new OrderSliceHarness(
                             _fixture, caller,
                             decorateOperationStore: store => boundary = new RefusingExecutionBoundaryOperationStore(store)))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => CancelAsync(refusing, order.Id, key));

                Assert.Empty(ReleaseKeys(refusing));
            }

            var refused = await OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            Assert.Equal(1, boundary!.Refusals);
            Assert.Equal(ServicingOperationStatus.Prepared, refused.Status);
            Assert.Null(await EvidenceOrNullAsync(_fixture, refused.Id, Stage));

            await using (var retrying = new OrderSliceHarness(_fixture, caller))
            {
                var retried = await CancelAsync(retrying, order.Id, key);

                Assert.Equal(refused.Id, retried.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, retried.OperationStatus);
                Assert.NotEmpty(ReleaseKeys(retrying));
            }
        }

        [Fact]
        public async Task DF_a_baseline_prepared_cancel_with_unresolved_release_evidence_recovers_before_any_second_release()
        {
            var order = await ReservedOrderAsync();
            var caller = Caller();
            var key = NewKey();

            await using (var suspending = new OrderSliceHarness(_fixture, caller))
            {
                suspending.Reservation.ReleaseOutcome = ProviderOperationOutcome.Unknown;

                var suspended = await CancelAsync(suspending, order.Id, key);

                Assert.Equal(ServicingOperationStatus.AwaitingExternal, suspended.OperationStatus);
            }

            var legacy = await OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);

            await DowngradeToPreparedAsync(_fixture, legacy.Id);
            await ExpireRecoveryLeaseAsync(_fixture, order.Id);

            await using (var resuming = new OrderSliceHarness(_fixture, caller))
            {
                resuming.Reservation.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

                var resumed = await CancelAsync(resuming, order.Id, key);

                Assert.Equal(legacy.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
                Assert.Empty(ReleaseKeys(resuming));
                Assert.NotEmpty(resuming.Reservation.ObservedRecoveryKeys);
            }
        }

        [Fact]
        public async Task DG_a_stale_snapshot_replay_of_a_completed_cancel_never_releases_twice()
        {
            var order = await ReservedOrderAsync();
            var caller = Caller();
            var key = NewKey();
            IReadOnlyList<string> releases = [];
            Order? cancelled = null;
            CompletingElsewhereOrderRepository? interleaving = null;

            await using var stale = new OrderSliceHarness(
                _fixture, caller,
                decorateOrders: orders => interleaving = new CompletingElsewhereOrderRepository(orders, async () =>
                {
                    await using var completing = new OrderSliceHarness(_fixture, caller);

                    var completed = await CancelAsync(completing, order.Id, key);

                    Assert.Equal(ServicingOperationStatus.Completed, completed.OperationStatus);

                    releases = ReleaseKeys(completing);
                    cancelled = await ReloadAsync(order.Id);
                }));

            var refusal = await Assert.ThrowsAsync<BusinessException>(() => CancelAsync(stale, order.Id, key));

            Assert.Equal(1, interleaving!.Completions);
            Assert.NotEmpty(releases);
            Assert.Equal(20334, refusal.Code);
            Assert.Empty(ReleaseKeys(stale));
            Assert.Empty(stale.Reservation.ObservedRecoveryKeys);

            var operation = await OperationAsync(_fixture, order.Id, ServicingOperationKind.Cancel);
            var after = await ReloadAsync(order.Id);

            Assert.Equal(ServicingOperationStatus.Completed, operation.Status);
            Assert.Equal(OrderStatus.Cancelled, after.Status);
            Assert.Equal(cancelled!.CommercialVersion, after.CommercialVersion);
            Assert.Single(after.Changes, change => change.ChangeType == OrderChangeType.Cancel);
            Assert.False(await ClaimIsBlockingAsync(_fixture, order.Id));

            await using (var retrying = new OrderSliceHarness(_fixture, caller))
            {
                var replay = await CancelAsync(retrying, order.Id, key);

                Assert.True(replay.IsReplay);
                Assert.Equal(ServicingOperationStatus.Completed, replay.OperationStatus);
                Assert.Empty(ReleaseKeys(retrying));
            }
        }

        private async Task<Order> ReservedOrderAsync()
        {
            await using var setup = new OrderSliceHarness(_fixture, Caller());

            await setup.SeedPlatformAsync();

            var order = await setup.CreateOrderAsync();

            await setup.Reserve.ReserveAsync(order.Id, NewKey(), null);

            return order;
        }

        private Task<Order> ReloadAsync(long orderId) => ExchangeScenarios.ReloadAsync(_fixture, orderId);

        private static Task<CancelOrderOutcome> CancelAsync(OrderSliceHarness harness, long orderId, string key)
            => harness.Cancel.CancelAsync(orderId, Reason, Actor, key, null);

        private static IReadOnlyList<string> ReleaseKeys(OrderSliceHarness harness)
            => harness.Reservation.ObservedOperationKeys
                .Where(key => key.StartsWith(ReleaseStep, StringComparison.Ordinal))
                .ToList();

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"release-boundary-{Guid.NewGuid():N}");
    }
}
