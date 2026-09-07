using AeroTech.Ordering.Domain.OrderAggregate;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public interface IInlineReservationService
    {
        Task<InlineReservationOutcome> ReserveAsync(
            Order order,
            string idempotencyKey,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);
    }
}
