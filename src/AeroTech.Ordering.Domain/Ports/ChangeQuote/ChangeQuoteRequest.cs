namespace AeroTech.Ordering.Domain.Ports.ChangeQuote
{
    public sealed record ChangeQuoteRequest(
        long OrderId,
        int CommercialVersion,
        long ElectronicTicketId,
        long OrderServiceId,
        long TicketCouponId,
        int SaleCurrencyId);
}
