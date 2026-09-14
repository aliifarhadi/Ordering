using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OperationCoordinatorInterleavingTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public OperationCoordinatorInterleavingTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_claim_that_appears_between_the_guard_read_and_the_acquire_refuses_the_second_worker()
        {
            var caller = TestCallerContexts.AirlineUser(7438, $"interleave-{Guid.NewGuid():N}");
            var key = Guid.NewGuid().ToString("N");

            await using var first = new OrderSliceHarness(_fixture, caller);
            await first.SeedPlatformAsync();
            var order = await first.CreateOrderAsync();
            var intent = new { OrderId = order.Id, Purpose = "interleaving" };

            var winner = await Coordinator(first, new OperationClaimStore(first.CommandContext, first.Ids, Clock))
                .BeginAsync(order.Id, ServicingOperationKind.VoidDocument, key, intent);

            Assert.Equal(1, winner.ClaimGeneration);

            await using var second = new OrderSliceHarness(_fixture, caller);

            var lateObserver = new ClaimHiddenFromGuard(
                new OperationClaimStore(second.CommandContext, second.Ids, Clock));

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => Coordinator(second, lateObserver)
                    .BeginAsync(order.Id, ServicingOperationKind.VoidDocument, key, intent));

            Assert.Equal(20076, error.Code);
            Assert.True(lateObserver.GuardReadHidAClaim);
        }

        [Fact]
        public async Task A_sequential_replay_of_a_quiescent_operation_is_still_allowed()
        {
            var caller = TestCallerContexts.AirlineUser(7438, $"interleave-{Guid.NewGuid():N}");
            var key = Guid.NewGuid().ToString("N");

            await using var first = new OrderSliceHarness(_fixture, caller);
            await first.SeedPlatformAsync();
            var order = await first.CreateOrderAsync();
            var intent = new { OrderId = order.Id, Purpose = "interleaving" };

            var suspended = await Coordinator(first, new OperationClaimStore(first.CommandContext, first.Ids, Clock))
                .BeginAsync(order.Id, ServicingOperationKind.VoidDocument, key, intent);

            await first.OperationStore.TransitionAsync(
                suspended.OperationId, ServicingOperationStatus.AwaitingExternal, suspended.ClaimGeneration);
            await first.CommandContext.SaveChangesAsync();

            await using var second = new OrderSliceHarness(_fixture, caller);

            var replay = await Coordinator(second, new OperationClaimStore(second.CommandContext, second.Ids, Clock))
                .BeginAsync(order.Id, ServicingOperationKind.VoidDocument, key, intent);

            Assert.Equal(suspended.OperationId, replay.OperationId);
            Assert.True(replay.IsReplay);
            Assert.Equal(suspended.ClaimGeneration + 1, replay.ClaimGeneration);
            Assert.Equal(replay.ClaimGeneration, (await ClaimRowAsync(replay.OperationId)).Generation);

            var stale = await Assert.ThrowsAsync<BusinessException>(
                () => Coordinator(first, new OperationClaimStore(first.CommandContext, first.Ids, Clock))
                    .ResolveAsync(order.Id, suspended));

            Assert.Equal(20072, stale.Code);

            await Coordinator(second, new OperationClaimStore(second.CommandContext, second.Ids, Clock))
                .ResolveAsync(order.Id, replay);
            await second.CommandContext.SaveChangesAsync();

            Assert.False((await ClaimRowAsync(replay.OperationId)).IsBlocking);
        }

        [Fact]
        public async Task A_refused_concurrent_worker_leaves_the_winners_fencing_generation_untouched()
        {
            var caller = TestCallerContexts.AirlineUser(7438, $"interleave-{Guid.NewGuid():N}");
            var key = Guid.NewGuid().ToString("N");

            await using var first = new OrderSliceHarness(_fixture, caller);
            await first.SeedPlatformAsync();
            var order = await first.CreateOrderAsync();
            var intent = new { OrderId = order.Id, Purpose = "interleaving" };

            var winnerCoordinator = Coordinator(first, new OperationClaimStore(first.CommandContext, first.Ids, Clock));
            var winner = await winnerCoordinator.BeginAsync(order.Id, ServicingOperationKind.VoidDocument, key, intent);
            var before = await ClaimRowAsync(winner.OperationId);

            await using var second = new OrderSliceHarness(_fixture, caller);

            var lateObserver = new ClaimHiddenFromGuard(
                new OperationClaimStore(second.CommandContext, second.Ids, Clock));

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => Coordinator(second, lateObserver)
                    .BeginAsync(order.Id, ServicingOperationKind.VoidDocument, key, intent));

            Assert.Equal(20076, error.Code);
            Assert.True(lateObserver.GuardReadHidAClaim);

            var after = await ClaimRowAsync(winner.OperationId);

            Assert.Equal(winner.ClaimGeneration, after.Generation);
            Assert.Equal(before.Generation, after.Generation);
            Assert.Equal(before.RecoveryLeaseUntil, after.RecoveryLeaseUntil);
            Assert.Equal(before.RowVersion, after.RowVersion);
            Assert.True(after.IsBlocking);

            await winnerCoordinator.ResolveAsync(order.Id, winner);
            await first.CommandContext.SaveChangesAsync();

            Assert.False((await ClaimRowAsync(winner.OperationId)).IsBlocking);
        }

        [Fact]
        public async Task A_different_operation_cannot_take_or_advance_a_live_claim()
        {
            var caller = TestCallerContexts.AirlineUser(7438, $"interleave-{Guid.NewGuid():N}");

            await using var first = new OrderSliceHarness(_fixture, caller);
            await first.SeedPlatformAsync();
            var order = await first.CreateOrderAsync();

            var owner = await Coordinator(first, new OperationClaimStore(first.CommandContext, first.Ids, Clock))
                .BeginAsync(
                    order.Id,
                    ServicingOperationKind.VoidDocument,
                    Guid.NewGuid().ToString("N"),
                    new { OrderId = order.Id, Purpose = "owner" });

            var before = await ClaimRowAsync(owner.OperationId);

            await using var second = new OrderSliceHarness(_fixture, caller);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => Coordinator(second, new OperationClaimStore(second.CommandContext, second.Ids, Clock))
                    .BeginAsync(
                        order.Id,
                        ServicingOperationKind.Cancel,
                        Guid.NewGuid().ToString("N"),
                        new { OrderId = order.Id, Purpose = "intruder" }));

            Assert.Equal(20070, error.Code);

            var after = await ClaimRowAsync(owner.OperationId);

            Assert.Equal(before.Generation, after.Generation);
            Assert.Equal(before.RowVersion, after.RowVersion);
            Assert.True(after.IsBlocking);
        }

        private async Task<OperationOrderClaim> ClaimRowAsync(long operationId)
        {
            await using var context = _fixture.NewCommandContext();

            return await context.Set<OperationOrderClaim>()
                .AsNoTracking()
                .SingleAsync(claim => claim.OperationId == operationId);
        }

        private static readonly OrderingDatabaseFixture.FixedClock Clock = new();

        private static OrderOperationCoordinator Coordinator(OrderSliceHarness harness, IOperationClaimStore claims)
            => new(
                harness.Receipts,
                claims,
                harness.OperationStore,
                Clock,
                Options.Create(new OrderOperationOptions
                {
                    RecoveryLeaseSeconds = 900,
                    TicketDocumentType = OrderSliceHarness.TicketDocumentType,
                    EmdDocumentType = OrderSliceHarness.EmdDocumentType
                }));

        private sealed class ClaimHiddenFromGuard : IOperationClaimStore
        {
            private readonly IOperationClaimStore _real;

            public ClaimHiddenFromGuard(IOperationClaimStore real) => _real = real;

            public bool GuardReadHidAClaim { get; private set; }

            public async Task<OperationClaim> AcquireAsync(
                long orderId, long operationId, DateTimeOffset recoveryLeaseUntil, CancellationToken cancellationToken = default)
                => await _real.AcquireAsync(orderId, operationId, recoveryLeaseUntil, cancellationToken);

            public async Task<OperationClaim> AcquireAsync(
                long orderId,
                long operationId,
                DateTimeOffset recoveryLeaseUntil,
                OperationClaim? observed,
                CancellationToken cancellationToken = default)
                => await _real.AcquireAsync(orderId, operationId, recoveryLeaseUntil, observed, cancellationToken);

            public async Task<OperationClaim?> FindBlockingAsync(long orderId, CancellationToken cancellationToken = default)
            {
                GuardReadHidAClaim = await _real.FindBlockingAsync(orderId, cancellationToken) is not null;

                return null;
            }

            public Task EnsureCurrentGenerationAsync(
                long orderId, long operationId, long generation, CancellationToken cancellationToken = default)
                => _real.EnsureCurrentGenerationAsync(orderId, operationId, generation, cancellationToken);

            public Task ResolveAsync(long orderId, long operationId, long generation, CancellationToken cancellationToken = default)
                => _real.ResolveAsync(orderId, operationId, generation, cancellationToken);
        }
    }
}
