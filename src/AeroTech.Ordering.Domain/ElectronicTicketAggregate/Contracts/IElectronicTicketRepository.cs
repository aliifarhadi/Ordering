namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts
{
    public interface IElectronicTicketRepository
    {
        Task<ElectronicTicket?> GetAsync(long id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ElectronicTicket>> ListByOrderAsync(long orderId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ElectronicTicket>> ListByOperationAsync(long operationId, CancellationToken cancellationToken = default);

        Task AddAsync(ElectronicTicket ticket, CancellationToken cancellationToken = default);
    }
}
