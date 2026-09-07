namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed record ReservedServiceLink(
        long OrderServiceId,
        string? HoldBatchId,
        string? SeatHoldReference);
}
