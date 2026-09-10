using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Query.OrderAggregate.Models;
using AeroTech.Ordering.Synchronizer.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Synchronizer
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class LocalTransactionAtomicityTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public LocalTransactionAtomicityTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task Command_and_query_rows_commit_in_one_transaction()
        {
            var id = NewId();

            await using (var command = _fixture.NewCommandContext())
            await using (var query = _fixture.NewQueryContext())
            {
                command.Set<CommandReceipt>().Add(Receipt(id));
                query.Orders.Add(ReadModel(id));

                await new OrderingUnitOfWork(command, query).SaveChangesAsync();
            }

            await using var commandVerification = _fixture.NewCommandContext();
            await using var queryVerification = _fixture.NewQueryContext();

            Assert.True(await commandVerification.Set<CommandReceipt>().AnyAsync(receipt => receipt.Id == id));
            Assert.True(await queryVerification.Orders.AnyAsync(order => order.Id == id));
        }

        [Fact]
        public async Task A_failing_query_projection_rolls_back_the_command_write()
        {
            var existingId = NewId();

            await using (var seed = _fixture.NewQueryContext())
            {
                seed.Orders.Add(ReadModel(existingId));
                await seed.SaveChangesAsync();
            }

            var receiptId = NewId();

            await using (var command = _fixture.NewCommandContext())
            await using (var query = _fixture.NewQueryContext())
            {
                command.Set<CommandReceipt>().Add(Receipt(receiptId));
                query.Orders.Add(ReadModel(existingId));

                await Assert.ThrowsAsync<DbUpdateException>(
                    () => new OrderingUnitOfWork(command, query).SaveChangesAsync());
            }

            await using var verification = _fixture.NewCommandContext();

            Assert.False(await verification.Set<CommandReceipt>().AnyAsync(receipt => receipt.Id == receiptId));
        }

        [Fact]
        public async Task A_failing_command_write_leaves_no_projected_row()
        {
            var receiptId = NewId();

            await using (var seed = _fixture.NewCommandContext())
            {
                seed.Set<CommandReceipt>().Add(Receipt(receiptId));
                await seed.SaveChangesAsync();
            }

            var readModelId = NewId();

            await using (var command = _fixture.NewCommandContext())
            await using (var query = _fixture.NewQueryContext())
            {
                command.Set<CommandReceipt>().Add(Receipt(receiptId));
                query.Orders.Add(ReadModel(readModelId));

                await Assert.ThrowsAsync<DbUpdateException>(
                    () => new OrderingUnitOfWork(command, query).SaveChangesAsync());
            }

            await using var verification = _fixture.NewQueryContext();

            Assert.False(await verification.Orders.AnyAsync(order => order.Id == readModelId));
        }

        private static CommandReceipt Receipt(long id) => new()
        {
            Id = id,
            OwnerAirlineId = 1,
            CallerScope = "test",
            OperationName = "TestOperation",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            RequestHash = "hash",
            Status = CommandReceiptStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        private static OrderReadModel ReadModel(long id) => new()
        {
            Id = id,
            UniqueIdentifierId = Guid.NewGuid(),
            Status = OrderStatus.Created,
            Type = OrderType.Normal,
            Channel = Messages.Shared.Enums.SalesChannel.BackOffice,
            CustomerId = 1,
            AirlineOfficeId = 1,
            CreatorUserId = 1,
            CurrencyId = 1,
            Pax = 1,
            CommercialVersion = 1,
            CreationDate = DateTimeOffset.UtcNow,
            LastProjectedAt = DateTimeOffset.UtcNow
        };

        private static long NewId() => DateTime.UtcNow.Ticks + Random.Shared.Next(1, 1_000_000);
    }
}
