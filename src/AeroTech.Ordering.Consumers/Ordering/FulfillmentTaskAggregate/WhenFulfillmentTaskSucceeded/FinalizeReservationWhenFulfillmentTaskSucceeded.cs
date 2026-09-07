using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Messages.Ordering.Enums;
using MassTransit;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.FulfillmentTaskSucceeded;

namespace AeroTech.Ordering.Consumers.Ordering.FulfillmentTaskAggregate.WhenFulfillmentTaskSucceeded
{
    public sealed class FinalizeReservationWhenFulfillmentTaskSucceeded : IConsumer<IntegrationEvent>
    {
        private readonly IOrderReservationService _reservationService;

        public FinalizeReservationWhenFulfillmentTaskSucceeded(IOrderReservationService reservationService)
            => _reservationService = reservationService;

        public async Task Consume(ConsumeContext<IntegrationEvent> context)
        {
            if (context.Message.TaskType != OrderFulfillmentTaskType.ReserveInventory)
                return;

            await _reservationService.FinalizeReservationAsync(context.Message.OrderId, context.CancellationToken);
        }
    }
}
