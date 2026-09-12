namespace AeroTech.Ordering.Domain.Ports.DocumentRevalidation
{
    public sealed record DocumentRevalidationRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string DocumentNumber,
        long TicketCouponId,
        int CouponNumber,
        string TargetSelectionRef);
}
