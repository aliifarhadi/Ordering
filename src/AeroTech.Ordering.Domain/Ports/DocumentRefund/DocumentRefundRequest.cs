namespace AeroTech.Ordering.Domain.Ports.DocumentRefund
{
    public sealed record DocumentRefundRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        IReadOnlyList<int> CouponNumbers);
}
