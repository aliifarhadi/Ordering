namespace AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts
{
    public interface IServicingReconciliationStore
    {
        Task<ServicingOperationSnapshot?> FindOperationAsync(
            long operationId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ServicingOperationSnapshot>> ListUnresolvedAsync(
            long orderId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ServicingDocumentEvidence>> ListDocumentsAsync(
            long orderId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ServicingControlEvidence>> ListControlAsync(
            long orderId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<ServicingReservationEvidence>> ListReservationsAsync(
            long orderId,
            CancellationToken cancellationToken = default);
    }
}
