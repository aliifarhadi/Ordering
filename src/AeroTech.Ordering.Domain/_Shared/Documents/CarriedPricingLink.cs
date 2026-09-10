namespace AeroTech.Ordering.Domain._Shared.Documents
{
    public sealed record CarriedPricingLink(
        long PricingLineId,
        int CouponNumber,
        decimal AttributedValue);
}
