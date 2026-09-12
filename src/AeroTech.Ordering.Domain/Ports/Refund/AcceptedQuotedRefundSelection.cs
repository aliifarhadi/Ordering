namespace AeroTech.Ordering.Domain.Ports.Refund
{
    public sealed record AcceptedQuotedRefundSelection(
        string OperationKey,
        long OrderId,
        long OperationId,
        string QuotedRefundId,
        int ExpectedCommercialVersion,
        long ElectronicTicketId,
        string DocumentNumber,
        IReadOnlyList<long> TicketCouponIds,
        int SaleCurrencyId);
}
