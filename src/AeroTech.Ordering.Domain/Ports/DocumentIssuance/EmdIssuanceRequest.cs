using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
    public sealed record EmdIssuanceRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        long? TravelerId,
        string DocumentNumber,
        ElectronicMiscDocumentType EmdType,
        string ReasonForIssuanceCode,
        long IssuerCarrierId,
        int CurrencyId,
        decimal TotalAmount,
        IReadOnlyList<EmdCouponRequest> Coupons);
}
