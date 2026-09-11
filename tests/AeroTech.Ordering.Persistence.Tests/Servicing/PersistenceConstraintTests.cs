using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Servicing
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class PersistenceConstraintTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public PersistenceConstraintTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task The_blocking_claim_index_is_unique_and_filtered()
        {
            await using var connection = new SqlConnection(OrderingDatabaseFixture.ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT i.is_unique, i.has_filter, i.filter_definition
                FROM sys.indexes i
                JOIN sys.tables t ON t.object_id = i.object_id
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = 'Order' AND t.name = 'OperationOrderClaims'
                  AND i.name = 'IX_OperationOrderClaims_OrderId'
                """;

            await using var reader = await command.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync(), "IX_OperationOrderClaims_OrderId is missing.");
            Assert.True(reader.GetBoolean(0), "the blocking-claim index is not unique");
            Assert.True(reader.GetBoolean(1), "the blocking-claim index is not filtered");
            Assert.Contains("IsBlocking", reader.GetString(2));
        }

        [Fact]
        public async Task The_receipt_uniqueness_key_covers_owner_scope_operation_and_key()
        {
            await using var connection = new SqlConnection(OrderingDatabaseFixture.ConnectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT c.name
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                JOIN sys.tables t ON t.object_id = i.object_id
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                WHERE s.name = 'Order' AND t.name = 'CommandReceipts'
                  AND i.is_unique = 1 AND i.is_primary_key = 0
                ORDER BY ic.key_ordinal
                """;

            var columns = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add(reader.GetString(0));

            Assert.Equal(
                new[] { "OwnerAirlineId", "CallerScope", "OperationName", "IdempotencyKey" },
                columns);
        }

        [Fact]
        public async Task An_unsaved_resolve_does_not_free_the_order()
        {
            var orderId = NewId();

            await using (var context = _fixture.NewCommandContext())
            {
                var store = NewStore(context);
                var claim = await store.AcquireAsync(orderId, 1, Lease);

                await store.ResolveAsync(orderId, 1, claim.Generation);
            }

            await using var next = _fixture.NewCommandContext();

            var error = await Assert.ThrowsAsync<BusinessException>(() => NewStore(next).AcquireAsync(orderId, 2, Lease));

            Assert.Equal(20070, error.Code);
        }

        [Fact]
        public async Task A_resolve_that_commits_with_the_finalization_frees_the_order()
        {
            var orderId = NewId();

            await using (var context = _fixture.NewCommandContext())
            {
                var store = NewStore(context);
                var claim = await store.AcquireAsync(orderId, 1, Lease);

                await store.ResolveAsync(orderId, 1, claim.Generation);
                await context.SaveChangesAsync();
            }

            await using var next = _fixture.NewCommandContext();
            var taken = await NewStore(next).AcquireAsync(orderId, 2, Lease);

            Assert.Equal(2, taken.OperationId);
        }

        [Fact]
        public async Task Resolving_with_a_stale_generation_is_rejected()
        {
            var orderId = NewId();

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            var first = await store.AcquireAsync(orderId, 1, Lease);
            await store.AcquireAsync(orderId, 1, Lease);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => store.ResolveAsync(orderId, 1, first.Generation));

            Assert.Equal(20072, error.Code);
        }

        [Fact]
        public async Task A_foreign_operation_cannot_resolve_another_operations_claim()
        {
            var orderId = NewId();

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            var claim = await store.AcquireAsync(orderId, 1, Lease);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => store.ResolveAsync(orderId, 2, claim.Generation));

            Assert.Equal(20071, error.Code);
        }

        [Fact]
        public async Task A_resolved_claim_is_retained_as_history()
        {
            var orderId = NewId();

            await using var context = _fixture.NewCommandContext();
            var store = NewStore(context);

            var claim = await store.AcquireAsync(orderId, 1, Lease);
            await store.ResolveAsync(orderId, 1, claim.Generation);
            await context.SaveChangesAsync();
            await store.AcquireAsync(orderId, 2, Lease);

            var rows = await context.Set<OperationOrderClaim>()
                .AsNoTracking()
                .Where(row => row.OrderId == orderId)
                .ToListAsync();

            Assert.Equal(2, rows.Count);
            Assert.Single(rows, row => row.IsBlocking);
            Assert.Single(rows, row => row.ResolvedAt is not null);
        }

        private static readonly DateTimeOffset Lease = new(2026, 9, 8, 11, 0, 0, TimeSpan.Zero);

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
