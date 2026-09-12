namespace AeroTech.Ordering.Domain.Ports.OrderChange
{
    public sealed record AcceptedQuotedOfferSelection(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedOfferId,
        string SelectedOfferItemId,
        int SaleCurrencyId);
}
