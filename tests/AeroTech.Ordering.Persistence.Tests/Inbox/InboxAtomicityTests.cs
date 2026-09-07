using AeroTech.Ordering.Persistence.Inbox;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Inbox
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class InboxAtomicityTests
    {
        private const string Consumer = "/DotAir.AeroTech.Ordering.Tests";

        private readonly OrderingDatabaseFixture _fixture;

        public InboxAtomicityTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task The_marker_and_the_consumer_effect_commit_together()
        {
            var messageId = Guid.NewGuid();
            var receiptId = NewId();

            await using (var context = _fixture.NewCommandContext())
            {
                var store = new InboxStore(context, new OrderingDatabaseFixture.FixedClock());

                store.EnlistProcessed(messageId, Consumer, "TestMessage");
                context.Set<CommandReceipt>().Add(Receipt(receiptId));

                await context.SaveChangesAsync();
            }

            await using var verification = _fixture.NewCommandContext();

            Assert.True(await verification.Set<InboxMessage>().AnyAsync(m => m.MessageId == messageId));
            Assert.True(await verification.Set<CommandReceipt>().AnyAsync(r => r.Id == receiptId));
        }

        [Fact]
        public async Task A_failing_consumer_leaves_no_marker_so_the_message_is_retried()
        {
            var messageId = Guid.NewGuid();

            await using (var context = _fixture.NewCommandContext())
            {
                var store = new InboxStore(context, new OrderingDatabaseFixture.FixedClock());

                store.EnlistProcessed(messageId, Consumer, "TestMessage");
                context.Set<CommandReceipt>().Add(Receipt(NewId(), operationName: new string('x', 200)));

                await Assert.ThrowsAnyAsync<Exception>(() => context.SaveChangesAsync());
            }

            await using var verification = _fixture.NewCommandContext();

            Assert.False(await verification.Set<InboxMessage>().AnyAsync(m => m.MessageId == messageId));
        }

        [Fact]
        public async Task A_concurrent_duplicate_delivery_cannot_commit_a_second_marker()
        {
            var messageId = Guid.NewGuid();

            await using var first = _fixture.NewCommandContext();
            await using var second = _fixture.NewCommandContext();

            new InboxStore(first, new OrderingDatabaseFixture.FixedClock()).EnlistProcessed(messageId, Consumer, "TestMessage");
            new InboxStore(second, new OrderingDatabaseFixture.FixedClock()).EnlistProcessed(messageId, Consumer, "TestMessage");

            await first.SaveChangesAsync();

            await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

            await using var verification = _fixture.NewCommandContext();

            Assert.Equal(1, await verification.Set<InboxMessage>().CountAsync(m => m.MessageId == messageId));
        }

        [Fact]
        public async Task An_ordinary_redelivery_is_recognised_by_the_fast_path()
        {
            var messageId = Guid.NewGuid();

            await using var context = _fixture.NewCommandContext();
            var store = new InboxStore(context, new OrderingDatabaseFixture.FixedClock());

            Assert.False(await store.HasProcessedAsync(messageId, Consumer));

            await store.MarkProcessedAsync(messageId, Consumer, "TestMessage");

            Assert.True(await store.HasProcessedAsync(messageId, Consumer));
        }

        private static CommandReceipt Receipt(long id, string operationName = "TestOperation") => new()
        {
            Id = id,
            OwnerAirlineId = 1,
            CallerScope = "test",
            OperationName = operationName,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = "hash",
            Status = Messages.Ordering.Enums.CommandReceiptStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        private static long NewId() => DateTime.UtcNow.Ticks + Random.Shared.Next(1, 100_000);
    }
}
