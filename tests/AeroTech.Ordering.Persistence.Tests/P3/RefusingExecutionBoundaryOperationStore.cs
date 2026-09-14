using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal sealed class RefusingExecutionBoundaryOperationStore : IServicingOperationStore
    {
        private readonly IServicingOperationStore _durable;

        public RefusingExecutionBoundaryOperationStore(IServicingOperationStore durable) => _durable = durable;

        public int Refusals { get; private set; }

        public Task<ServicingOperationRecord> PrepareAsync(
            long operationId,
            long orderId,
            ServicingOperationKind kind,
            string requestHash,
            long claimGeneration,
            long? commandReceiptId = null,
            int? expectedCommercialVersion = null,
            CancellationToken cancellationToken = default)
            => _durable.PrepareAsync(
                operationId, orderId, kind, requestHash, claimGeneration, commandReceiptId, expectedCommercialVersion,
                cancellationToken);

        public Task TransitionAsync(
            long operationId,
            ServicingOperationStatus status,
            long claimGeneration,
            CancellationToken cancellationToken = default)
            => status == ServicingOperationStatus.Executing
                ? Refuse()
                : _durable.TransitionAsync(operationId, status, claimGeneration, cancellationToken);

        public Task BeginExecutionAsync(
            long operationId,
            long claimGeneration,
            CancellationToken cancellationToken = default)
            => Refuse();

        public Task<ServicingOperationRecord?> FindAsync(long operationId, CancellationToken cancellationToken = default)
            => _durable.FindAsync(operationId, cancellationToken);

        private Task Refuse()
        {
            Refusals++;

            throw new InvalidOperationException("The execution boundary could not be recorded.");
        }
    }
}
