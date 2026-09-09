namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public sealed record RefundExecution(
        long OrderId,
        long ElectronicTicketId,
        IReadOnlyList<long> TicketCouponIds,
        string IdempotencyKey,
        int? ExpectedCommercialVersion,
        string? QuotedRefundId = null,
        ManualRefundInstruction? Manual = null)
    {
        public bool IsManual => Manual is not null;
    }
}
