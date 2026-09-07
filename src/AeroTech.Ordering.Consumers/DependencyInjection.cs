using AeroTech.Ordering.Consumers.Ordering.OrderAggregate.WhenOrderDocumentVoided;
using AeroTech.Ordering.Consumers.Jobs;
using AeroTech.Ordering.Consumers.Ordering.FulfillmentTaskAggregate.WhenFulfillmentTaskFailed;
using AeroTech.Ordering.Consumers.Ordering.FulfillmentTaskAggregate.WhenFulfillmentTaskSucceeded;
using AeroTech.Ordering.Consumers.Ordering.FulfillmentTaskAggregate.WhenIssueTicketFailed;
using AeroTech.Ordering.Consumers.Ordering.FulfillmentTaskAggregate.WhenIssueTicketSucceeded;
using AeroTech.Ordering.Consumers.Ordering.OrderAggregate.WhenOrderCreated;
using AeroTech.Ordering.Consumers.Inbox;
using AeroTech.Ordering.Consumers.Ordering.OrderAggregate.WhenOrderReservationUnconfirmed;
using AeroTech.Ordering.Consumers.Outbox;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AeroTech.Ordering.Consumers
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddConsumers(this IServiceCollection services, IConfiguration configuration)
        {
            var broker = configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();

            ArgumentException.ThrowIfNullOrWhiteSpace(broker.Server, "RabbitMq:Server");
            ArgumentException.ThrowIfNullOrWhiteSpace(broker.UserName, "RabbitMq:UserName");
            ArgumentException.ThrowIfNullOrWhiteSpace(broker.Password, "RabbitMq:Password");

            services.AddScoped(typeof(InboxConsumeFilter<>));

            services.AddMassTransit(bus =>
            {
                bus.SetKebabCaseEndpointNameFormatter();
                bus.AddConsumer<SyncQueryDbWhenOrderCreated>();
                bus.AddConsumer<FinalizeReservationWhenFulfillmentTaskSucceeded>();
                bus.AddConsumer<FailReservationWhenFulfillmentTaskFailed>();
                bus.AddConsumer<FinalizeIssuanceWhenIssueTicketSucceeded>();
                bus.AddConsumer<FailIssuanceWhenIssueTicketFailed>();
                bus.AddConsumer<AlertWhenOrderReservationUnconfirmed>();
                bus.AddConsumer<ReverseSaleWhenOrderDocumentVoided>();

                bus.UsingRabbitMq((context, rabbit) =>
                {
                    rabbit.Host(broker.Server, broker.Port, broker.VirtualHost, configurator =>
                    {
                        configurator.Username(broker.UserName);
                        configurator.Password(broker.Password);
                    });

                    rabbit.UseConsumeFilter(typeof(InboxConsumeFilter<>), context);

                    rabbit.ReceiveEndpoint("DotAir.AeroTech.Ordering.QuerySynchronizer", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Immediate(3));
                        endpoint.ConfigureConsumer<SyncQueryDbWhenOrderCreated>(context);
                    });

                    rabbit.ReceiveEndpoint("DotAir.AeroTech.Ordering.Reservation", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Immediate(3));
                        endpoint.ConfigureConsumer<FinalizeReservationWhenFulfillmentTaskSucceeded>(context);
                        endpoint.ConfigureConsumer<FailReservationWhenFulfillmentTaskFailed>(context);
                    });

                    rabbit.ReceiveEndpoint("DotAir.AeroTech.Ordering.Issuance", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Immediate(3));
                        endpoint.ConfigureConsumer<FinalizeIssuanceWhenIssueTicketSucceeded>(context);
                        endpoint.ConfigureConsumer<FailIssuanceWhenIssueTicketFailed>(context);
                    });

                    rabbit.ReceiveEndpoint("DotAir.AeroTech.Ordering.Notifications", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Immediate(3));
                        endpoint.ConfigureConsumer<AlertWhenOrderReservationUnconfirmed>(context);
                    });

                    rabbit.ReceiveEndpoint("DotAir.AeroTech.Ordering.Accounting", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Immediate(3));
                        endpoint.ConfigureConsumer<ReverseSaleWhenOrderDocumentVoided>(context);
                    });
                });
            });

            services.Configure<OutboxPublisherOptions>(configuration.GetSection("Outbox"));
            services.Configure<MessageRetentionOptions>(configuration.GetSection("MessageRetention"));
            services.AddHostedService<OutboxPublisher>();
            services.AddHostedService<MessageRetentionPoller>();
            services.AddHostedService<FulfillmentTaskPoller>();
            services.AddHostedService<OrderExpiryPoller>();

            return services;
        }
    }
}
