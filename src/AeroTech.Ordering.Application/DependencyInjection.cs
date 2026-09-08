using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Behaviors;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Adapters;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Application.OrderAggregate.Access;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer;
using AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota;
using AeroTech.Ordering.Application.OrderAggregate.Services.Cancel;
using AeroTech.Ordering.Application.OrderAggregate.Services.Expiry;
using AeroTech.Ordering.Application.OrderAggregate.Services.Creation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Ordering.Application.OrderAggregate.Services.Payment;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Split;
using AeroTech.Ordering.Application.TrafficDocumentAggregate;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Application.OrderAggregate.Services.Creation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Issuance;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.Ordering.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
        {
            var assembly = typeof(DependencyInjection).Assembly;

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
            services.AddValidatorsFromAssembly(assembly);

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

            services.Configure<FulfillmentOptions>(configuration.GetSection("Fulfillment"));
            services.Configure<ExpiryOptions>(configuration.GetSection("Expiry"));
            services.AddScoped<IFulfillmentPlanner, FulfillmentPlanner>();
            services.AddScoped<IFulfillmentProviderAdapter, AirlineReserveInventoryAdapter>();
            services.AddScoped<IFulfillmentProviderAdapter, AirlineIssueTicketAdapter>();
            services.AddScoped<IFulfillmentProviderAdapter, VoidTicketAdapter>();
            services.AddScoped<IFulfillmentProviderAdapter, AirlineReleaseInventoryAdapter>();
            services.AddScoped<IFulfillmentProviderResolver, FulfillmentProviderResolver>();
            services.AddScoped<IFulfillmentExecutor, FulfillmentExecutor>();
            services.AddScoped<IFulfillmentTaskRunner, FulfillmentTaskRunner>();
            services.AddScoped<IFulfillmentService, FulfillmentService>();

            services.Configure<RecordLocatorOptions>(configuration.GetSection("RecordLocator"));
            services.AddScoped<IRecordLocatorAllocator, RecordLocatorAllocator>();
            services.AddScoped<IReservationApplier, ReservationApplier>();
            services.AddScoped<IOrderReservationService, OrderReservationService>();
            services.AddScoped<IInlineReservationService, InlineReservationService>();
            services.AddScoped<ICreateOrderFromOfferService, CreateOrderFromOfferService>();
            services.AddScoped<IPaymentService, PaymentService>();
            services.AddScoped<ITrafficDocumentVoidService, TrafficDocumentVoidService>();
            services.AddScoped<IOrderCancelService, OrderCancelService>();

            services.Configure<OtaOptions>(configuration.GetSection("Ota"));

            services.Configure<IssuanceOptions>(configuration.GetSection("Issuance"));
            services.Configure<TicketNumberOptions>(configuration.GetSection("TicketNumber"));
            services.AddSingleton<ITicketNumberGenerator, TicketNumberGenerator>();
            services.AddScoped<ITicketNumberAllocator, TicketNumberAllocator>();
            services.AddScoped<IOrderIssuanceService, OrderIssuanceService>();
            services.AddScoped<IOrderExpiryService, OrderExpiryService>();
            services.AddScoped<IOrderSplitService, OrderSplitService>();

            services.Configure<OrderOperationOptions>(configuration.GetSection(OrderOperationOptions.SectionName));
            services.AddScoped<IOrderOperationCoordinator, OrderOperationCoordinator>();
            services.AddScoped<ICreateOrderService, CreateOrderService>();
            services.AddScoped<IReserveOrderService, ReserveOrderService>();
            services.AddScoped<IElectronicTicketIssuer, ElectronicTicketIssuer>();
            services.AddScoped<IElectronicMiscDocumentIssuer, ElectronicMiscDocumentIssuer>();
            services.AddScoped<IIssueOrderService, IssueOrderService>();
            services.AddScoped<IWithdrawOrderService, WithdrawOrderService>();
            services.AddScoped<IOrderChangeService, OrderChangeService>();
            services.AddScoped<IOrderCustomerAccessGuard, OrderCustomerAccessGuard>();

            return services;
        }
    }
}
