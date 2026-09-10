using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record ElectronicMiscDocumentIssuedCoupon(
        long EmdCouponId,
        int CouponNumber,
        EmdCouponPurpose Purpose,
        long? OrderServiceId,
        long? AssociatedTicketCouponId,
        decimal IssuanceValue);
}
