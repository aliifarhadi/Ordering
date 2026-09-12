namespace AeroTech.Ordering.Domain.Ports.Refund
{
    public sealed record RefundQuoteRequest(
        long OrderId,
        int CommercialVersion,
        long ElectronicTicketId,
        string DocumentNumber,
        IReadOnlyList<long> TicketCouponIds,
        int SaleCurrencyId);
}
