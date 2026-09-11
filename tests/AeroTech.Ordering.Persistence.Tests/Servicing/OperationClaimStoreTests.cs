using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Servicing
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OperationClaimStoreTests
    {
        private static readonly DateTimeOffset Lease = new(2026, 9, 8, 11, 0, 0, TimeSpan.Zero);

        private readonly OrderingDatabaseFixture _fixture;

        public OperationClaimStoreTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_second_operation_cannot_claim_a_claimed_order()
        {
            var orderId = NewId();

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            await store.AcquireAsync(orderId, 1, Lease);

            var error = await Assert.ThrowsAsync<BusinessException>(() => store.AcquireAsync(orderId, 2, Lease));

            Assert.Equal(20070, error.Code);
            Assert.Equal(409, error.HttpStatus);
        }

        [Fact]
        public async Task Recovery_by_the_same_operation_advances_the_fencing_generation()
        {
            var orderId = NewId();

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            var first = await store.AcquireAsync(orderId, 1, Lease);
            var second = await store.AcquireAsync(orderId, 1, Lease.AddHours(1));

            Assert.Equal(1, first.Generation);
            Assert.Equal(2, second.Generation);
        }

        [Fact]
        public async Task Finalizing_with_a_stale_generation_is_rejected()
        {
            var orderId = NewId();

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            var first = await store.AcquireAsync(orderId, 1, Lease);
            await store.AcquireAsync(orderId, 1, Lease);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => store.EnsureCurrentGenerationAsync(orderId, 1, first.Generation));

            Assert.Equal(20072, error.Code);
        }

        [Fact]
        public async Task An_expired_lease_does_not_release_an_unresolved_claim()
        {
            var orderId = NewId();

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            await store.AcquireAsync(orderId, 1, new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));

            var error = await Assert.ThrowsAsync<BusinessException>(() => store.AcquireAsync(orderId, 2, Lease));

            Assert.Equal(20070, error.Code);
        }

        [Fact]
        public async Task A_resolved_claim_frees_the_order_for_the_next_operation()
        {
            var orderId = NewId();

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            var claim = await store.AcquireAsync(orderId, 1, Lease);
            await store.ResolveAsync(orderId, 1, claim.Generation);
            await context.SaveChangesAsync();

            var next = await store.AcquireAsync(orderId, 2, Lease);

            Assert.Equal(2, next.OperationId);
            Assert.Equal(1, next.Generation);
        }

        [Fact]
        public async Task Concurrent_operations_produce_exactly_one_blocking_claim()
        {
            var orderId = NewId();

            var attempts = Enumerable.Range(1, 6).Select(operationId => Task.Run(async () =>
            {
                await using var context = _fixture.NewCommandContext();

                try
                {
                    await NewStore(context).AcquireAsync(orderId, operationId, Lease);
                    return true;
                }
                catch (BusinessException error) when (error.Code == 20070)
                {
                    return false;
                }
            }));

            var results = await Task.WhenAll(attempts);

            await using var verification = _fixture.NewCommandContext();
            var blocking = await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .CountAsync(claim => claim.OrderId == orderId && claim.IsBlocking);

            Assert.Equal(1, results.Count(won => won));
            Assert.Equal(1, blocking);
        }

        private static OperationClaimStore NewStore(OrderingDbContext context)
            => new(context, new TestIdGenerator(), new OrderingDatabaseFixture.FixedClock());

        private static long NewId() => DateTime.UtcNow.Ticks + Random.Shared.Next(1, 100_000);

        private sealed class TestIdGenerator : IIdGenerator
        {
            private long _next = DateTime.UtcNow.Ticks;

            public long NewId() => Interlocked.Increment(ref _next);
        }
    }
}
