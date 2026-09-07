using AeroTech.Ordering.ReferenceData.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.ReferenceData.Persistence
{
    public sealed class ReferenceDbContext : DbContext
    {
        public const string Schema = "ReferenceData";
        public const string MigrationsHistorySchema = "dbo";
        public const string MigrationsHistoryTable = "__ReferenceDataMigrationHistory";

        public ReferenceDbContext(DbContextOptions<ReferenceDbContext> options) : base(options)
        {
        }

        public DbSet<CurrencyReadModel> Currencies => Set<CurrencyReadModel>();
        public DbSet<AirlineReadModel> Airlines => Set<AirlineReadModel>();
        public DbSet<CityReadModel> Cities => Set<CityReadModel>();
        public DbSet<AirportReadModel> Airports => Set<AirportReadModel>();
        public DbSet<AirportTerminalReadModel> AirportTerminals => Set<AirportTerminalReadModel>();
        public DbSet<CustomerReadModel> Customers => Set<CustomerReadModel>();
        public DbSet<OperatorSettingsReadModel> OperatorSettings => Set<OperatorSettingsReadModel>();
        public DbSet<ReferenceSyncState> SyncStates => Set<ReferenceSyncState>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.Entity<CurrencyReadModel>(entity =>
            {
                entity.ToTable("Currencies");
                entity.HasKey(currency => currency.Id);
                entity.Property(currency => currency.Id).ValueGeneratedNever();
                entity.Property(currency => currency.Code).HasMaxLength(8);
            });

            modelBuilder.Entity<AirlineReadModel>(entity =>
            {
                entity.ToTable("Airlines");
                entity.HasKey(airline => airline.Id);
                entity.Property(airline => airline.Id).ValueGeneratedNever();
                entity.Property(airline => airline.IataCode).HasMaxLength(8);
                entity.HasIndex(airline => airline.IataCode);
            });

            modelBuilder.Entity<CityReadModel>(entity =>
            {
                entity.ToTable("Cities");
                entity.HasKey(city => city.Id);
                entity.Property(city => city.Id).ValueGeneratedNever();
                entity.Property(city => city.IataCode).HasMaxLength(8);
                entity.HasIndex(city => city.IataCode);
            });

            modelBuilder.Entity<AirportReadModel>(entity =>
            {
                entity.ToTable("Airports");
                entity.HasKey(airport => airport.Id);
                entity.Property(airport => airport.Id).ValueGeneratedNever();
                entity.Property(airport => airport.IataCode).HasMaxLength(8);
                entity.Property(airport => airport.IkaoCode).HasMaxLength(8);
                entity.HasIndex(airport => airport.IataCode);
            });

            modelBuilder.Entity<AirportTerminalReadModel>(entity =>
            {
                entity.ToTable("AirportTerminals");
                entity.HasKey(terminal => terminal.Id);
                entity.Property(terminal => terminal.Id).ValueGeneratedNever();
                entity.Property(terminal => terminal.Number).HasMaxLength(16);
                entity.HasIndex(terminal => terminal.AirportId);
            });

            modelBuilder.Entity<CustomerReadModel>(entity =>
            {
                entity.ToTable("Customers");
                entity.HasKey(customer => customer.Id);
                entity.Property(customer => customer.Id).ValueGeneratedNever();
                entity.Property(customer => customer.UniqueIdentifier).HasMaxLength(64);
                entity.HasIndex(customer => customer.UniqueIdentifier);
            });

            modelBuilder.Entity<OperatorSettingsReadModel>(entity =>
            {
                entity.ToTable("OperatorSettings");
                entity.HasKey(settings => settings.Id);
                entity.Property(settings => settings.Id).ValueGeneratedNever();
                entity.Property(settings => settings.ScopeKey).HasMaxLength(64).IsRequired();
                entity.HasIndex(settings => settings.ScopeKey).IsUnique();
            });

            modelBuilder.Entity<ReferenceSyncState>(entity =>
            {
                entity.ToTable("ReferenceDataSyncStates");
                entity.HasKey(state => state.Id);
                entity.Property(state => state.Id).HasMaxLength(64).ValueGeneratedNever();
            });

            base.OnModelCreating(modelBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<string>().HaveMaxLength(256);
        }
    }
}
