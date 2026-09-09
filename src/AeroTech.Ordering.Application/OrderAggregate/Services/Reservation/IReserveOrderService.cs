namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public interface IReserveOrderService
    {
        Task<ReserveOrderOutcome> ReserveAsync(
            long orderId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }
}
