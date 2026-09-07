namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate.ValueObjects
{
    public sealed record DocumentAmounts(
        decimal Fare,
        decimal TaxesTotal,
        decimal FeesTotal,
        decimal Commission,
        decimal TotalAmount,
        int CurrencyId);
}
