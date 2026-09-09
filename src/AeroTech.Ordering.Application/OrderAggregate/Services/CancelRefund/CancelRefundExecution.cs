namespace AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund
{
    public sealed record CancelRefundExecution(
        long OrderId,
        long ElectronicTicketId,
        long RefundRecordId,
        string Reason,
        string IdempotencyKey,
        int? ExpectedCommercialVersion,
        string? ReasonDetail = null);
}
