using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record AffectedAncillaryAssociation(
        ElectronicMiscDocument Document,
        EmdCoupon Coupon,
        TicketCoupon PredecessorCoupon);
}
