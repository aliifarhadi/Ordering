using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
    public interface IEmdIssuancePort
    {
        Task<DocumentIssuanceResult> IssueAsync(EmdIssuanceRequest request, CancellationToken cancellationToken = default);

        Task<DocumentIssuanceResult> RecoverAsync(DocumentRecoveryRequest request, CancellationToken cancellationToken = default);
    }

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

    public sealed record EmdCouponRequest(
        int CouponNumber,
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        decimal AttributedValue,
        long? OrderServiceId = null,
        string? AssociatedTicketDocumentNumber = null,
        int? AssociatedTicketCouponNumber = null,
        string? ExternalValueReference = null);
}
