namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts
{
    public interface ITrafficDocumentRepository
    {
        Task AddAsync(TrafficDocument document, CancellationToken cancellationToken = default);

        Task<TrafficDocument?> GetAsync(long id, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TrafficDocument>> GetByOrderAsync(long orderId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<TrafficDocument>> GetByOrderAndTravellersAsync(long orderId, IReadOnlyCollection<long> travellerIds, CancellationToken cancellationToken = default);

        Task<bool> DocumentNumberExistsAsync(string documentNumber, CancellationToken cancellationToken = default);
    }
}
