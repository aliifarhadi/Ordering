using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public interface IOrderReservationService
    {
        Task<FulfillmentOutcome> ReserveAsync(long orderId, CancellationToken cancellationToken = default);

        Task<OrderStatus> FinalizeReservationAsync(long orderId, CancellationToken cancellationToken = default);

        Task<OrderStatus> FailReservationAsync(long orderId, string reason, CancellationToken cancellationToken = default);
    }
}
