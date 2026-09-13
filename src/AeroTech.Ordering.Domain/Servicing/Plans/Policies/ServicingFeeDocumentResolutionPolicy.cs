using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Domain.Servicing.Plans.Policies
{
    public static class ServicingFeeDocumentResolutionPolicy
    {
        public static ResolvedServicingFeeDocument? Resolve(
            IReadOnlyCollection<OrderPricingLine> committedLines,
            long priceChangeSetId,
            AcceptedExchangeFeeDocument document)
        {
            ArgumentNullException.ThrowIfNull(committedLines);
            ArgumentNullException.ThrowIfNull(document);

            var coupons = new List<ResolvedServicingFeeCoupon>();

            foreach (var coupon in document.Coupons)
            {
                if (Line(committedLines, priceChangeSetId, coupon.PrimarySourceLineRef) is not { } primary)
                    return null;

                var links = new List<EmdCouponPriceLink>();

                foreach (var attribution in coupon.Attributions)
                {
                    if (Line(committedLines, priceChangeSetId, attribution.SourceLineRef) is not { } line)
                        return null;

                    links.Add(new EmdCouponPriceLink(line.Id, null, attribution.AttributedAmount));
                }

                coupons.Add(new ResolvedServicingFeeCoupon(coupon, primary.Id, links));
            }

            return new ResolvedServicingFeeDocument(document, coupons);
        }

        private static OrderPricingLine? Line(
            IReadOnlyCollection<OrderPricingLine> committedLines,
            long priceChangeSetId,
            string sourceLineRef)
        {
            var matches = committedLines
                .Where(line => line.PriceChangeSetId == priceChangeSetId
                               && string.Equals(line.SourceLineRef, sourceLineRef, StringComparison.Ordinal))
                .ToList();

            return matches.Count == 1 ? matches[0] : null;
        }
    }
}
