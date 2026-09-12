namespace AeroTech.Ordering.Domain.Ports.ChangeQuote
{
    public sealed record AcceptedQuotedChangeSelection(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedChangeId,
        int ExpectedCommercialVersion,
        long ElectronicTicketId,
        long OrderServiceId,
        long TicketCouponId,
        int SaleCurrencyId);
}
