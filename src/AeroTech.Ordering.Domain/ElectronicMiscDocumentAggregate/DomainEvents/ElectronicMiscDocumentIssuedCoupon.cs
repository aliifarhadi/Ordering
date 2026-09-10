using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.DomainEvents
{
    public sealed record ElectronicMiscDocumentIssuedCoupon(
        long EmdCouponId,
        int CouponNumber,
        EmdCouponPurpose Purpose,
        long? OrderServiceId,
        long? AssociatedTicketCouponId,
        decimal IssuanceValue);
}
