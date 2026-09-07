namespace AeroTech.Ordering.Domain.OrderAggregate.Dto
{
    
    public sealed record OrderIssuancePlan(IReadOnlyList<TravellerTicketPlan> Travellers);

    public sealed record TravellerTicketPlan(
        long TravellerId,
        TicketAmountBreakdown Amounts,
        IReadOnlyList<ServiceCouponPlan> Coupons);

    public sealed record ServiceCouponPlan(
        long OrderServiceId,
        long OrderSegmentId,
        TicketAmountBreakdown Amounts);

    public sealed record TicketAmountBreakdown(
        decimal Fare,
        decimal TaxesTotal,
        decimal FeesTotal,
        decimal Commission,
        decimal TotalAmount,
        int CurrencyId);
}
