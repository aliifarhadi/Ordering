using AeroTech.Ordering.Query.OrderAggregate.Models;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Query._Shared.DbContexts
{
    public sealed class OrderQueryDbContext : DbContext
    {
        public const string ReadModelSchema = "ReadModel";
        public const string MigrationsHistorySchema = "dbo";
        public const string MigrationsHistoryTable = "__QueriesMigrationHistory";

        public const string CommandSchema = "Order";

        public OrderQueryDbContext(DbContextOptions<OrderQueryDbContext> options) : base(options)
        {
        }

        public DbSet<OrderReadModel> Orders => Set<OrderReadModel>();

        public DbSet<OrderDetailsReadModel> OrderDetails => Set<OrderDetailsReadModel>();

        public DbSet<OrderTravellerReadModel> OrderTravellers => Set<OrderTravellerReadModel>();

        public DbSet<OrderFlightReadModel> OrderFlights => Set<OrderFlightReadModel>();

        public DbSet<CustomerReadModel> Customers => Set<CustomerReadModel>();

        public DbSet<CurrencyReadModel> Currencies => Set<CurrencyReadModel>();

        public DbSet<AirportReadModel> Airports => Set<AirportReadModel>();

        public DbSet<AirlineReadModel> Airlines => Set<AirlineReadModel>();

        public DbSet<ServicingOperationReadModel> ServicingOperations => Set<ServicingOperationReadModel>();

        public DbSet<CommandReceiptReadModel> CommandReceipts => Set<CommandReceiptReadModel>();

        public DbSet<ServicingExternalEvidenceReadModel> ServicingExternalEvidences
            => Set<ServicingExternalEvidenceReadModel>();

        public DbSet<ServicingManualResolutionReadModel> ServicingManualResolutions
            => Set<ServicingManualResolutionReadModel>();

        public DbSet<ElectronicTicketReadModel> ElectronicTickets => Set<ElectronicTicketReadModel>();

        public DbSet<TicketCouponReadModel> TicketCoupons => Set<TicketCouponReadModel>();

        public DbSet<ElectronicMiscDocumentReadModel> ElectronicMiscDocuments
            => Set<ElectronicMiscDocumentReadModel>();

        public DbSet<FulfillmentReservationReadModel> FulfillmentReservations
            => Set<FulfillmentReservationReadModel>();

        public DbSet<FulfillmentReservationServiceReadModel> FulfillmentReservationServices
            => Set<FulfillmentReservationServiceReadModel>();

        public DbSet<AcceptedExchangePlanReadModel> AcceptedExchangePlans
            => Set<AcceptedExchangePlanReadModel>();

        public DbSet<AcceptedExchangePlanAncillaryReadModel> AcceptedExchangePlanAncillaries
            => Set<AcceptedExchangePlanAncillaryReadModel>();

        public DbSet<AcceptedExchangePlanFeeDocumentReadModel> AcceptedExchangePlanFeeDocuments
            => Set<AcceptedExchangePlanFeeDocumentReadModel>();

        public DbSet<AcceptedExchangePlanAncillaryExchangeGroupReadModel> AcceptedExchangePlanAncillaryExchangeGroups
            => Set<AcceptedExchangePlanAncillaryExchangeGroupReadModel>();

        public DbSet<AcceptedExchangePlanAncillaryCancelGroupReadModel> AcceptedExchangePlanAncillaryCancelGroups
            => Set<AcceptedExchangePlanAncillaryCancelGroupReadModel>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(ReadModelSchema);
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrderQueryDbContext).Assembly);

            // Reference read models are owned/migrated by ReferenceDbContext (ReferenceData schema);
            // mapped here read-only so order-search can JOIN their names.
            MapReferenceReadModel<CustomerReadModel>(modelBuilder, "Customers");
            MapReferenceReadModel<CurrencyReadModel>(modelBuilder, "Currencies");
            MapReferenceReadModel<AirportReadModel>(modelBuilder, "Airports");
            MapReferenceReadModel<AirlineReadModel>(modelBuilder, "Airlines");

            MapCommandReadModel<ServicingOperationReadModel>(modelBuilder, "ServicingOperations", "Id");
            MapCommandReadModel<CommandReceiptReadModel>(modelBuilder, "CommandReceipts", "Id");
            MapCommandReadModel<ServicingExternalEvidenceReadModel>(
                modelBuilder, "ServicingExternalEvidences", "OperationId", "Stage");
            MapCommandReadModel<ServicingManualResolutionReadModel>(
                modelBuilder, "ServicingManualResolutions", "OperationId", "ResolutionId");
            MapCommandReadModel<ElectronicTicketReadModel>(modelBuilder, "ElectronicTickets", "Id");
            MapCommandReadModel<TicketCouponReadModel>(modelBuilder, "TicketCoupons", "Id");
            MapCommandReadModel<ElectronicMiscDocumentReadModel>(modelBuilder, "ElectronicMiscDocuments", "Id");
            MapCommandReadModel<FulfillmentReservationReadModel>(modelBuilder, "FulfillmentReservations", "Id");
            MapCommandReadModel<FulfillmentReservationServiceReadModel>(
                modelBuilder, "FulfillmentReservationServices", "Id");
            MapCommandReadModel<AcceptedExchangePlanReadModel>(
                modelBuilder, "AcceptedExchangePlans", "OperationId");
            MapCommandReadModel<AcceptedExchangePlanAncillaryReadModel>(
                modelBuilder, "AcceptedExchangePlanAncillaries", "OperationId", "EmdCouponId");
            MapCommandReadModel<AcceptedExchangePlanFeeDocumentReadModel>(
                modelBuilder, "AcceptedExchangePlanFeeDocuments", "OperationId", "DocumentReference");
            MapCommandReadModel<AcceptedExchangePlanAncillaryExchangeGroupReadModel>(
                modelBuilder, "AcceptedExchangePlanAncillaryExchangeGroups", "OperationId", "ExchangeGroupRef");
            MapCommandReadModel<AcceptedExchangePlanAncillaryCancelGroupReadModel>(
                modelBuilder, "AcceptedExchangePlanAncillaryCancelGroups", "OperationId", "CancelGroupRef");
        }

        private static void MapCommandReadModel<TEntity>(
            ModelBuilder modelBuilder,
            string table,
            params string[] keys)
            where TEntity : class
            => modelBuilder.Entity<TEntity>(entity =>
            {
                entity.ToTable(table, CommandSchema, builder => builder.ExcludeFromMigrations());
                entity.HasKey(keys);
            });

        private static void MapReferenceReadModel<TEntity>(ModelBuilder modelBuilder, string table)
            where TEntity : class
            => modelBuilder.Entity<TEntity>(entity =>
            {
                entity.ToTable(table, ReferenceDbContext.Schema, builder => builder.ExcludeFromMigrations());
                entity.HasKey("Id");
            });

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
            configurationBuilder.Properties<string>().HaveMaxLength(256);
        }
    }
}
