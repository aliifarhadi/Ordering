namespace AeroTech.Ordering.Domain.DocumentStockAggregate.Contracts
{
    public interface IDocumentStockRepository
    {
        Task<DocumentStock?> GetActiveAsync(long ownerAirlineId, string documentType, CancellationToken cancellationToken = default);

        Task<DocumentStock?> GetAsync(long id, CancellationToken cancellationToken = default);

        Task AddAsync(DocumentStock stock, CancellationToken cancellationToken = default);
    }
}
