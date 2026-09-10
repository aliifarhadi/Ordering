namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record SuccessorDocumentPriceLink(
        string SourceLineRef,
        decimal AttributedValue,
        int CurrencyId,
        long? AllocationId = null);
}
