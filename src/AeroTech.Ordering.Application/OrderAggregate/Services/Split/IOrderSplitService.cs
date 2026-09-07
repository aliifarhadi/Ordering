namespace AeroTech.Ordering.Application.OrderAggregate.Services.Split
{
    public interface IOrderSplitService
    {
        Task<SplitOrderResult> SplitAsync(long sourceOrderId, IReadOnlyCollection<long> travellerIds, CancellationToken cancellationToken = default);
    }
}
