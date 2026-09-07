using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public interface IReservationApplier
    {
        Task<string> CompleteAsync(Order order, FulfillmentTask reserveTask, CancellationToken cancellationToken = default);

        Task FailAsync(Order order, string reason, CancellationToken cancellationToken = default);

        Task MarkUnconfirmedAsync(Order order, FulfillmentTask reserveTask, FulfillmentFailureReason reason, string detail, CancellationToken cancellationToken = default);
    }
}
