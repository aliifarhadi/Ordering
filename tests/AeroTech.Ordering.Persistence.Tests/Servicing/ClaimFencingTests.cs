using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Servicing
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ClaimFencingTests
    {
        private static readonly DateTimeOffset Lease = new(2026, 9, 8, 11, 0, 0, TimeSpan.Zero);

        private readonly OrderingDatabaseFixture _fixture;

        public ClaimFencingTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task Two_recovery_workers_for_the_same_operation_cannot_both_own_the_current_generation()
        {
            var orderId = NewId();
            const long operationId = 4242;

            await using (var owner = _fixture.NewCommandContext())
                await NewStore(owner).AcquireAsync(orderId, operationId, Lease);

            await using var first = _fixture.NewCommandContext();
            await using var second = _fixture.NewCommandContext();

            var firstStore = NewStore(first);
            var secondStore = NewStore(second);

            var firstTask = firstStore.AcquireAsync(orderId, operationId, Lease.AddHours(1));
            var secondTask = secondStore.AcquireAsync(orderId, operationId, Lease.AddHours(2));

            var outcomes = await Task.WhenAll(Capture(firstTask), Capture(secondTask));

            var winners = outcomes.Where(outcome => outcome.Claim is not null).ToList();
            var losers = outcomes.Where(outcome => outcome.Error is not null).ToList();

            Assert.Single(winners);
            Assert.Single(losers);
            Assert.Equal(2706, losers[0].Error!.Code);

            await using var verification = _fixture.NewCommandContext();
            var stored = await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .SingleAsync(claim => claim.OrderId == orderId && claim.IsBlocking);

            Assert.Equal(2, stored.Generation);
            Assert.Equal(stored.Generation, winners[0].Claim!.Generation);
        }

        [Fact]
        public async Task Only_one_blocking_claim_survives_concurrent_same_operation_first_acquisition()
        {
            var orderId = NewId();
            const long operationId = 5252;

            var attempts = Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
            {
                await using var context = _fixture.NewCommandContext();
                return await Capture(NewStore(context).AcquireAsync(orderId, operationId, Lease));
            }));

            var outcomes = await Task.WhenAll(attempts);

            await using var verification = _fixture.NewCommandContext();
            var claims = await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .Where(claim => claim.OrderId == orderId)
                .ToListAsync();

            Assert.Single(claims);
            Assert.True(claims[0].IsBlocking);
            Assert.Equal(claims.Single().Generation, outcomes.Where(o => o.Claim is not null).Max(o => o.Claim!.Generation));
            Assert.All(outcomes.Where(o => o.Error is not null), o => Assert.Contains(o.Error!.Code, new[] { 2700, 2706 }));
        }

        [Fact]
        public async Task A_worker_that_lost_the_race_fails_the_generation_check()
        {
            var orderId = NewId();
            const long operationId = 6262;

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            var stale = await store.AcquireAsync(orderId, operationId, Lease);
            var current = await store.AcquireAsync(orderId, operationId, Lease);

            await store.EnsureCurrentGenerationAsync(orderId, operationId, current.Generation);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => store.EnsureCurrentGenerationAsync(orderId, operationId, stale.Generation));

            Assert.Equal(2702, error.Code);
        }

        [Fact]
        public async Task A_stale_generation_cannot_resolve_the_claim_after_a_newer_worker_took_over()
        {
            var orderId = NewId();
            const long operationId = 7272;

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            var stale = await store.AcquireAsync(orderId, operationId, Lease);
            await store.AcquireAsync(orderId, operationId, Lease);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => store.ResolveAsync(orderId, operationId, stale.Generation));

            Assert.Equal(2702, error.Code);

            await using var verification = _fixture.NewCommandContext();
            Assert.True(await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .AnyAsync(claim => claim.OrderId == orderId && claim.IsBlocking));
        }

        private static async Task<Outcome> Capture(Task<Domain.Servicing.Operations.Contracts.OperationClaim> task)
        {
            try
            {
                return new Outcome(await task, null);
            }
            catch (BusinessException error)
            {
                return new Outcome(null, error);
            }
        }

        private sealed record Outcome(Domain.Servicing.Operations.Contracts.OperationClaim? Claim, BusinessException? Error);

        private static OperationClaimStore NewStore(OrderingDbContext context)
            => new(context, new TestIdGenerator(), new OrderingDatabaseFixture.FixedClock());

        private static long NewId() => DateTime.UtcNow.Ticks + Random.Shared.Next(1, 1_000_000);

        private sealed class TestIdGenerator : IIdGenerator
        {
            private long _next = DateTime.UtcNow.Ticks;

            public long NewId() => Interlocked.Increment(ref _next);
        }
    }
}
