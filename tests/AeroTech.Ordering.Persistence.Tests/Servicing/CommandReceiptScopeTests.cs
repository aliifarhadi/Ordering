using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Operations
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class CommandReceiptScopeTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public CommandReceiptScopeTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task One_caller_reusing_a_key_for_one_operation_cannot_create_a_second_receipt()
        {
            var key = Guid.NewGuid().ToString("N");
            const string scope = "OtaPanel|TravelAgency:11|Subject:subject-a";

            await using var context = _fixture.NewCommandContext();

            context.Set<CommandReceipt>().Add(Receipt(scope, key));
            await context.SaveChangesAsync();

            context.Set<CommandReceipt>().Add(Receipt(scope, key));

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Theory]
        [InlineData("OtaPanel|TravelAgency:11|Subject:subject-a", "OtaPanel|TravelAgency:22|Subject:subject-a")]
        [InlineData("OtaPanel|TravelAgency:11|Subject:subject-a", "OtaPanel|TravelAgency:11|Subject:subject-b")]
        [InlineData("OtaPanel|TravelAgency:11|Subject:subject-a", "Api|TravelAgency:11|Subject:subject-a")]
        [InlineData("Api|TravelAgency:11|Client:client-1", "Api|TravelAgency:11|Client:client-2")]
        [InlineData("Backoffice|AirlineOffice:11|Subject:subject-a", "OtaPanel|TravelAgency:11|Subject:subject-a")]
        public async Task Different_caller_scopes_never_collide_on_the_same_key(string first, string second)
        {
            var key = Guid.NewGuid().ToString("N");

            await using var context = _fixture.NewCommandContext();

            context.Set<CommandReceipt>().Add(Receipt(first, key));
            context.Set<CommandReceipt>().Add(Receipt(second, key));

            await context.SaveChangesAsync();

            var stored = await context.Set<CommandReceipt>()
                .AsNoTracking()
                .CountAsync(receipt => receipt.IdempotencyKey == key);

            Assert.Equal(2, stored);
        }

        [Fact]
        public async Task Two_owner_airlines_never_collide_on_the_same_caller_scope_and_key()
        {
            var key = Guid.NewGuid().ToString("N");
            const string scope = "Service|Service|Client:ordering-worker";

            await using var context = _fixture.NewCommandContext();

            context.Set<CommandReceipt>().Add(Receipt(scope, key, ownerAirlineId: 1));
            context.Set<CommandReceipt>().Add(Receipt(scope, key, ownerAirlineId: 2));

            await context.SaveChangesAsync();

            Assert.Equal(2, await context.Set<CommandReceipt>().AsNoTracking()
                .CountAsync(receipt => receipt.IdempotencyKey == key));
        }

        [Fact]
        public async Task One_caller_may_reuse_a_key_across_different_operations()
        {
            var key = Guid.NewGuid().ToString("N");
            const string scope = "OtaPanel|TravelAgency:11|Subject:subject-a";

            await using var context = _fixture.NewCommandContext();

            context.Set<CommandReceipt>().Add(Receipt(scope, key, operationName: "CreateOrder"));
            context.Set<CommandReceipt>().Add(Receipt(scope, key, operationName: "CancelOrder"));

            await context.SaveChangesAsync();

            Assert.Equal(2, await context.Set<CommandReceipt>().AsNoTracking()
                .CountAsync(receipt => receipt.IdempotencyKey == key));
        }

        private static CommandReceipt Receipt(
            string callerScope,
            string idempotencyKey,
            long ownerAirlineId = 1,
            string operationName = "CreateOrder") => new()
        {
            Id = DateTime.UtcNow.Ticks + Random.Shared.Next(1, 1_000_000),
            OwnerAirlineId = ownerAirlineId,
            CallerScope = callerScope,
            OperationName = operationName,
            IdempotencyKey = idempotencyKey,
            RequestHash = "hash",
            Status = CommandReceiptStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }
}
