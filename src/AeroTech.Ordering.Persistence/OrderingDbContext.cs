using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Framework.Infrastructure.Persistence;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.PaymentAggregate;
using AeroTech.Ordering.Domain.ProviderInteractionAggregate;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate;
using AeroTech.Ordering.Persistence.Inbox;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence
{
    public sealed class OrderingDbContext : CommandDbContext, IUnitOfWork
    {
        public const string MigrationsHistorySchema = "dbo";
        public const string MigrationsHistoryTable = "__CommandsMigrationHistory";

        public OrderingDbContext(
            DbContextOptions<OrderingDbContext> options,
            IIdentityService identityService,
            IClock clock,
            IDomainEventDispatcher domainEventDispatcher)
            : base(options, identityService, clock, domainEventDispatcher)
        {
        }

        public DbSet<Order> Orders => Set<Order>();

        public DbSet<FulfillmentTask> FulfillmentTasks => Set<FulfillmentTask>();

        public DbSet<Payment> Payments => Set<Payment>();

        public DbSet<TrafficDocument> TrafficDocuments => Set<TrafficDocument>();

        public DbSet<ProviderInteraction> ProviderInteractions => Set<ProviderInteraction>();

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

        public DbSet<CommandReceipt> CommandReceipts => Set<CommandReceipt>();

        public DbSet<ServicingOperation> ServicingOperations => Set<ServicingOperation>();

        public DbSet<OperationOrderClaim> OperationOrderClaims => Set<OperationOrderClaim>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("Order");
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderingDbContext).Assembly);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
            configurationBuilder.Properties<string>().HaveMaxLength(256);
        }
    }
}
