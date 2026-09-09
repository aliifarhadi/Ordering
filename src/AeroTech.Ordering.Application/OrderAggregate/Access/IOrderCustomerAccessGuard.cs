namespace AeroTech.Ordering.Application.OrderAggregate.Access
{
    public interface IOrderCustomerAccessGuard
    {
        long RequireCustomerId();

        Task EnsureOwnedAsync(long orderId, CancellationToken cancellationToken = default);
    }
}
