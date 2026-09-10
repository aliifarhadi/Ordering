using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Ordering.Domain.Servicing.Plans.Contracts;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Framework.Infrastructure.HealthChecks;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.PaymentAggregate.Contracts;
using AeroTech.Ordering.Domain.ProviderInteractionAggregate.Contracts;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using AeroTech.Ordering.Persistence.DocumentStockAggregate;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.FulfillmentReservationAggregate;
using AeroTech.Ordering.Persistence.FulfillmentTaskAggregate;
using AeroTech.Ordering.Persistence.Inbox;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Outbox;
using AeroTech.Ordering.Persistence.PaymentAggregate;
using AeroTech.Ordering.Persistence.ProviderInteractionAggregate;
using AeroTech.Ordering.Persistence.TrafficDocumentAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.Ordering.Persistence
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("CommandDbContext")
                                   ?? configuration.GetConnectionString("OrderingDbContext");

            services.AddDbContext<OrderingDbContext>(options => options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(OrderingDbContext.MigrationsHistoryTable, OrderingDbContext.MigrationsHistorySchema)));
            services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<OrderingDbContext>());
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddSingleton<IRecordLocatorGenerator, RecordLocatorGenerator>();
            services.AddScoped<IFulfillmentTaskRepository, FulfillmentTaskRepository>();
            services.AddScoped<IPaymentRepository, PaymentRepository>();
            services.AddScoped<ITrafficDocumentRepository, TrafficDocumentRepository>();
            services.AddScoped<IProviderInteractionRepository, ProviderInteractionRepository>();
            services.AddScoped<IFulfillmentReservationRepository, FulfillmentReservationRepository>();
            services.AddScoped<IDocumentStockRepository, DocumentStockRepository>();
            services.AddScoped<IElectronicTicketRepository, ElectronicTicketRepository>();
            services.AddScoped<AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts.IElectronicMiscDocumentRepository, ElectronicMiscDocumentAggregate.ElectronicMiscDocumentRepository>();
            services.Configure<IntegrationEventOptions>(configuration.GetSection("IntegrationEvents"));
            services.AddScoped<IOutboxWriter, OutboxWriter>();
            services.AddScoped<IInboxStore, InboxStore>();
            services.AddScoped<IOperationClaimStore, OperationClaimStore>();
            services.AddScoped<ICommandReceiptStore, CommandReceiptStore>();
            services.AddScoped<IServicingOperationStore, ServicingOperationStore>();
            services.AddScoped<IAcceptedChangePlanStore, AcceptedChangePlanStore>();
            services.AddScoped<IAcceptedExchangePlanStore, AcceptedExchangePlanStore>();

            services.AddHealthChecks().AddDbContextReadinessCheck<OrderingDbContext>("sql-server-command");

            return services;
        }
    }
}
