namespace AeroTech.Ordering.Domain.DocumentStockAggregate.Contracts
{
    public interface IDocumentStockRepository
    {
        Task<DocumentStock?> GetActiveForOperationAsync(
            long ownerAirlineId,
            string documentType,
            long operationId,
            CancellationToken cancellationToken = default);

        Task<DocumentStock?> GetAsync(long id, CancellationToken cancellationToken = default);

        Task AddAsync(DocumentStock stock, CancellationToken cancellationToken = default);
    }
}
