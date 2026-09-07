using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Messages.Ordering.Enums;
using MassTransit;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.FulfillmentTaskFailed;

namespace AeroTech.Ordering.Consumers.Ordering.FulfillmentTaskAggregate.WhenFulfillmentTaskFailed
{
    public sealed class FailReservationWhenFulfillmentTaskFailed : IConsumer<IntegrationEvent>
    {
        private readonly IOrderReservationService _reservationService;

        public FailReservationWhenFulfillmentTaskFailed(IOrderReservationService reservationService)
            => _reservationService = reservationService;

        public async Task Consume(ConsumeContext<IntegrationEvent> context)
        {
            if (context.Message.TaskType != OrderFulfillmentTaskType.ReserveInventory)
                return;

            await _reservationService.FailReservationAsync(context.Message.OrderId, context.Message.Error, context.CancellationToken);
        }
    }
}
