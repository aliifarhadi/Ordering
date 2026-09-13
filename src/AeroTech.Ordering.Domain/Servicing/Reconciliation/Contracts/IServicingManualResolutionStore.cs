namespace AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts
{
    public interface IServicingManualResolutionStore
    {
        Task<ServicingManualResolution?> FindAsync(
            long operationId,
            string resolutionId,
            CancellationToken cancellationToken = default);

        Task<ServicingManualResolution> AppendAsync(
            ServicingManualResolution resolution,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ServicingManualResolution>> ListAsync(
            long operationId,
            CancellationToken cancellationToken = default);
    }
}
