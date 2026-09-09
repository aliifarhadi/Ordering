namespace AeroTech.Ordering.Application.OrderAggregate.Operations
{
    public sealed record OrderOperation(
        long ReceiptId,
        long OperationId,
        long ClaimGeneration,
        bool IsReplay);
}
