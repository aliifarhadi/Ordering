namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record ResolvedServicingFeeDocument(
        AcceptedExchangeFeeDocument Document,
        IReadOnlyList<ResolvedServicingFeeCoupon> Coupons)
    {
        public int LinkCount => Coupons.Sum(coupon => coupon.PriceLinks.Count);
    }
}
