using System.Security.Claims;
using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Persistence;
using AeroTech.Ordering.Query._Shared.DbContexts;
using AeroTech.Ordering.ReferenceData.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests._Shared
{
    public sealed class OrderingDatabaseFixture : IDisposable
    {
        public const string ConnectionString =
            @"Server=localhost\SQLEXPRESS;Database=DotAirOrderNewP0Tests;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";

        public OrderingDatabaseFixture()
        {
            using var command = NewCommandContext();
            command.Database.Migrate();

            using var reference = NewReferenceContext();
            reference.Database.Migrate();

            using var query = NewQueryContext();
            query.Database.Migrate();

            ResetIssuedDocumentState(command);
        }

        private static void ResetIssuedDocumentState(OrderingDbContext command)
        {
            command.Database.ExecuteSqlRaw(
                """
                DELETE FROM [Order].[EmdPriceLinks];
                DELETE FROM [Order].[EmdCoupons];
                DELETE FROM [Order].[ElectronicMiscDocuments];
                DELETE FROM [Order].[DocumentPriceLinks];
                DELETE FROM [Order].[TicketCoupons];
                DELETE FROM [Order].[ElectronicTickets];
                DELETE FROM [Order].[DocumentStockAllocations];
                DELETE FROM [Order].[DocumentStocks];
                """);
        }

        public OrderingDbContext NewCommandContext() => NewCommandContext(new NullDomainEventDispatcher());

        public OrderingDbContext NewCommandContext(IDomainEventDispatcher dispatcher)
        {
            var options = new DbContextOptionsBuilder<OrderingDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            return new OrderingDbContext(options, new NullIdentityService(), new FixedClock(), dispatcher);
        }

        public OrderQueryDbContext NewQueryContext()
        {
            var options = new DbContextOptionsBuilder<OrderQueryDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            return new OrderQueryDbContext(options);
        }

        public ReferenceDbContext NewReferenceContext()
        {
            var options = new DbContextOptionsBuilder<ReferenceDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            return new ReferenceDbContext(options);
        }

        public void Dispose()
        {
        }

        public sealed class FixedClock : IClock
        {
            public DateTimeOffset GetDateTime() => new(2026, 9, 8, 10, 0, 0, TimeSpan.Zero);

            public DateOnly GetDate() => new(2026, 9, 8);
        }

        public sealed class NullDomainEventDispatcher : IDomainEventDispatcher
        {
            public Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }

        public sealed class NullIdentityService : IIdentityService
        {
            public long? CurrentUserId => 1;

            public long? CurrentCustomerId => 1;

            public long RequiredCurrentUserId => 1;

            public Guid RequiredDeviceId => Guid.Empty;

            public bool IsAuthenticated => true;

            public List<Claim>? Claims => null;

            public void CheckAccess(string scopeType, object scopeId)
            {
            }
        }
    }

    [CollectionDefinition(Name)]
    public sealed class OrderingDatabaseCollection : ICollectionFixture<OrderingDatabaseFixture>
    {
        public const string Name = "OrderingDatabase";
    }
}
