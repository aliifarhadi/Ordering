namespace AeroTech.Ordering.Domain.Servicing.Operations.Contracts
{
    public interface IOperationClaimStore
    {
        Task<OperationClaim> AcquireAsync(long orderId, long operationId, DateTimeOffset recoveryLeaseUntil, CancellationToken cancellationToken = default);

        Task<OperationClaim?> FindBlockingAsync(long orderId, CancellationToken cancellationToken = default);

        Task EnsureCurrentGenerationAsync(long orderId, long operationId, long generation, CancellationToken cancellationToken = default);

        Task ResolveAsync(long orderId, long operationId, long generation, CancellationToken cancellationToken = default);
    }
}
