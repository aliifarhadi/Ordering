using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Operations.Contracts
{
    public sealed record ServicingOperationRecord(
        long OperationId,
        long OwnerAirlineId,
        long OrderId,
        ServicingOperationKind Kind,
        ServicingOperationStatus Status,
        long ClaimGeneration);

    public interface IServicingOperationStore
    {
        Task<ServicingOperationRecord> PrepareAsync(
            long operationId,
            long orderId,
            ServicingOperationKind kind,
            string requestHash,
            long claimGeneration,
            long? commandReceiptId = null,
            int? expectedCommercialVersion = null,
            CancellationToken cancellationToken = default);

        Task TransitionAsync(
            long operationId,
            ServicingOperationStatus status,
            long claimGeneration,
            CancellationToken cancellationToken = default);

        Task<ServicingOperationRecord?> FindAsync(long operationId, CancellationToken cancellationToken = default);
    }
}
