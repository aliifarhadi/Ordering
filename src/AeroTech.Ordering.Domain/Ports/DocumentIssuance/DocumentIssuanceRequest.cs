namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
    public sealed record DocumentIssuanceRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        long TravelerId,
        string DocumentNumber,
        long IssuerCarrierId,
        int CurrencyId,
        decimal TotalAmount,
        IReadOnlyList<DocumentCouponRequest> Coupons);
}
