using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Query.OrderAggregate.Models;
using AeroTech.Ordering.Synchronizer.OrderAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Synchronizer
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class SharedTransactionTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public SharedTransactionTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task Both_contexts_end_up_on_one_physical_connection()
        {
            await using var command = _fixture.NewCommandContext();
            await using var query = _fixture.NewQueryContext();

            command.Set<CommandReceipt>().Add(Receipt(NewId()));
            query.Orders.Add(ReadModel(NewId()));

            await new OrderingUnitOfWork(command, query).SaveChangesAsync();

            Assert.Same(command.Database.GetDbConnection(), query.Database.GetDbConnection());
        }

        [Fact]
        public async Task The_unit_of_work_leaves_no_transaction_open_on_the_query_context()
        {
            await using var command = _fixture.NewCommandContext();
            await using var query = _fixture.NewQueryContext();

            command.Set<CommandReceipt>().Add(Receipt(NewId()));

            await new OrderingUnitOfWork(command, query).SaveChangesAsync();

            Assert.Null(query.Database.CurrentTransaction);
            Assert.Null(command.Database.CurrentTransaction);
        }

        [Fact]
        public async Task Repeated_saves_in_one_scope_each_commit_independently()
        {
            await using var command = _fixture.NewCommandContext();
            await using var query = _fixture.NewQueryContext();
            var unitOfWork = new OrderingUnitOfWork(command, query);

            var firstId = NewId();
            command.Set<CommandReceipt>().Add(Receipt(firstId));
            query.Orders.Add(ReadModel(firstId));
            await unitOfWork.SaveChangesAsync();

            var secondId = NewId();
            command.Set<CommandReceipt>().Add(Receipt(secondId));
            query.Orders.Add(ReadModel(secondId));
            await unitOfWork.SaveChangesAsync();

            await using var verification = _fixture.NewQueryContext();

            Assert.True(await verification.Orders.AnyAsync(order => order.Id == firstId));
            Assert.True(await verification.Orders.AnyAsync(order => order.Id == secondId));
        }

        [Fact]
        public async Task A_caller_owned_transaction_governs_the_commit()
        {
            var id = NewId();

            await using var command = _fixture.NewCommandContext();
            await using var query = _fixture.NewQueryContext();

            query.Database.SetDbConnection(command.Database.GetDbConnection(), contextOwnsConnection: false);

            await using (var ambient = await command.Database.BeginTransactionAsync())
            {
                command.Set<CommandReceipt>().Add(Receipt(id));
                query.Orders.Add(ReadModel(id));

                await new OrderingUnitOfWork(command, query).SaveChangesAsync();

                await ambient.RollbackAsync();
            }

            await using var verification = _fixture.NewCommandContext();

            Assert.False(await verification.Set<CommandReceipt>().AnyAsync(receipt => receipt.Id == id));
        }

        [Fact]
        public async Task A_rolled_back_projection_leaves_the_read_model_untouched()
        {
            var orderId = NewId();

            await using (var seed = _fixture.NewQueryContext())
            {
                seed.Orders.Add(ReadModel(orderId));
                await seed.SaveChangesAsync();
            }

            var receiptId = NewId();

            await using (var seed = _fixture.NewCommandContext())
            {
                seed.Set<CommandReceipt>().Add(Receipt(receiptId));
                await seed.SaveChangesAsync();
            }

            await using (var command = _fixture.NewCommandContext())
            await using (var query = _fixture.NewQueryContext())
            {
                var existing = await query.Orders.SingleAsync(order => order.Id == orderId);
                existing.CommercialVersion = 99;

                command.Set<CommandReceipt>().Add(Receipt(receiptId));

                await Assert.ThrowsAsync<DbUpdateException>(
                    () => new OrderingUnitOfWork(command, query).SaveChangesAsync());
            }

            await using var verification = _fixture.NewQueryContext();
            var stored = await verification.Orders.AsNoTracking().SingleAsync(order => order.Id == orderId);

            Assert.Equal(1, stored.CommercialVersion);
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
