namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record AcceptedSuccessorCoupon(
        decimal IssuanceValue,
        string? FareBasis,
        IReadOnlyList<SuccessorDocumentPriceLink> PriceLinks);
}
