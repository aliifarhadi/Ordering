namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public interface ITicketNumberAllocator
    {
        Task<string> AllocateAsync(CancellationToken cancellationToken = default);
    }
}
