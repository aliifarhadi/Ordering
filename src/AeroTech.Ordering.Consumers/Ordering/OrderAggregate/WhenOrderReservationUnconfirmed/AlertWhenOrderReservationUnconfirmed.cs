using MassTransit;
using Microsoft.Extensions.Logging;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderReservationUnconfirmed;

namespace AeroTech.Ordering.Consumers.Ordering.OrderAggregate.WhenOrderReservationUnconfirmed
{
    public sealed class AlertWhenOrderReservationUnconfirmed : IConsumer<IntegrationEvent>
    {
        private readonly ILogger<AlertWhenOrderReservationUnconfirmed> _logger;

        public AlertWhenOrderReservationUnconfirmed(ILogger<AlertWhenOrderReservationUnconfirmed> logger)
            => _logger = logger;

        public Task Consume(ConsumeContext<IntegrationEvent> context)
        {
            var @event = context.Message;

            _logger.LogWarning(
                "Order {OrderId} reservation is unconfirmed and requires manual retry. Reason: {Reason}. Customer: {CustomerId}. Office: {AirlineOfficeId}. Detail: {Detail}",
                @event.OrderId,
                @event.Reason,
                @event.CustomerId,
                @event.AirlineOfficeId,
                @event.Detail);

            return Task.CompletedTask;
        }
    }
}
