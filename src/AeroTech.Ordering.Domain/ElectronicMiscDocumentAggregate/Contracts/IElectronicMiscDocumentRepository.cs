namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts
{
    public interface IElectronicMiscDocumentRepository
    {
        Task<IReadOnlyList<ElectronicMiscDocument>> ListByOrderAsync(long orderId, CancellationToken cancellationToken = default);

        Task<ElectronicMiscDocument?> GetAsync(long id, CancellationToken cancellationToken = default);

        Task AddAsync(ElectronicMiscDocument document, CancellationToken cancellationToken = default);
    }
}
