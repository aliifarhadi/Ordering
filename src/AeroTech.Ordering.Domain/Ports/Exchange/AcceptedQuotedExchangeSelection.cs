namespace AeroTech.Ordering.Domain.Ports.Exchange
{
    public sealed record AcceptedQuotedExchangeSelection(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedExchangeId,
        int ExpectedCommercialVersion,
        long PredecessorElectronicTicketId,
        IReadOnlyList<long> ChangedOrderServiceIds,
        int SaleCurrencyId);
}
