using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record ResolvedServicingFeeCoupon(
        AcceptedServicingFeeDocumentCoupon Accepted,
        long PrimaryPricingLineId,
        IReadOnlyList<EmdCouponPriceLink> PriceLinks);
}
