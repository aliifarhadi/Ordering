using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain._Shared.Operations.Contracts
{
    public sealed record CommandReceiptResult(
        long ReceiptId,
        long OwnerAirlineId,
        string CallerScope,
        string OperationName,
        string IdempotencyKey,
        CommandReceiptStatus Status,
        bool IsReplay,
        long? OrderId,
        long OperationId);

    public interface ICommandReceiptStore
    {
        Task AttachOrderAsync(long receiptId, long orderId, CancellationToken cancellationToken = default);

        Task SetStatusAsync(long receiptId, CommandReceiptStatus status, CancellationToken cancellationToken = default);

        Task<CommandReceiptResult> AcquireAsync(
            string operationName,
            string idempotencyKey,
            string requestHash,
            CancellationToken cancellationToken = default);
    }
}
