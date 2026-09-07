namespace AeroTech.Ordering.Domain.OrderAggregate.Contracts
{
    public interface IOrderProjector
    {
        Task ProjectAsync(long orderId, CancellationToken cancellationToken = default);
    }
}
