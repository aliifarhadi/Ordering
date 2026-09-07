using AeroTech.Ordering.ReferenceData.AirInfo;
using AeroTech.Ordering.ReferenceData.Configuration;
using AeroTech.Ordering.ReferenceData.Core;
using AeroTech.Ordering.ReferenceData.Persistence;
using AeroTech.Ordering.ReferenceData.Syncing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AeroTech.Ordering.ReferenceData
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddReferenceData(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ReferenceDataOptions>(configuration.GetSection(ReferenceDataOptions.SectionName));

            var connectionString = configuration.GetConnectionString("QueryDbContext")
                                   ?? configuration.GetConnectionString("CommandDbContext")
                                   ?? configuration.GetConnectionString("OrderingDbContext");

            services.AddDbContext<ReferenceDbContext>(options => options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(ReferenceDbContext.MigrationsHistoryTable, ReferenceDbContext.MigrationsHistorySchema)));

            var airInfoBaseUrl = configuration["AirInfo:BaseUrl"];
            services.AddHttpClient<IAirInfoClient, AirInfoClient>(client =>
            {
                if (!string.IsNullOrWhiteSpace(airInfoBaseUrl))
                    client.BaseAddress = new Uri(airInfoBaseUrl);
            });

            var coreBaseUrl = configuration["AeroCore:BaseUrl"];
            services.AddHttpClient<ICoreClient, CoreClient>(client =>
            {
                if (!string.IsNullOrWhiteSpace(coreBaseUrl))
                    client.BaseAddress = new Uri(coreBaseUrl);
            });

            services.TryAddSingleton(TimeProvider.System);

            services.AddScoped<CurrencySyncer>();
            services.AddScoped<AirlineSyncer>();
            services.AddScoped<CitySyncer>();
            services.AddScoped<AirportSyncer>();
            services.AddScoped<CustomerSyncer>();
            services.AddScoped<OperatorSettingsSyncer>();

            return services;
        }
    }
}
