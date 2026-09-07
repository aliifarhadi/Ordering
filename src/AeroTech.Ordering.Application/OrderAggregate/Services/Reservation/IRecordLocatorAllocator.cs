namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public interface IRecordLocatorAllocator
    {
        Task<string> AllocateAsync(CancellationToken cancellationToken = default);
    }
}
