namespace AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange
{
    public sealed record SelectedQuotedOffer(string QuotedOfferId, IReadOnlyList<string> SelectedOfferItemIds);
}
