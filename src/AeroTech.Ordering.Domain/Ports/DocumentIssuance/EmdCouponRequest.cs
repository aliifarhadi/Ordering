using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentIssuance
{
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
