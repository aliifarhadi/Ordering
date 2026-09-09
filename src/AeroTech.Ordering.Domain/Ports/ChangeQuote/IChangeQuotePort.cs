using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;

namespace AeroTech.Ordering.Domain.Ports.ChangeQuote
{
    public interface IChangeQuotePort
    {
        Task<OrderAggregate.AcceptedSource.VoluntaryChange.ChangeQuote> QuoteAsync(
            ChangeQuoteRequest request,
            CancellationToken cancellationToken = default);

        Task<AcceptedVoluntaryChange> AcceptQuotedChangeAsync(
            AcceptedQuotedChangeSelection selection,
            CancellationToken cancellationToken = default);
    }

    public sealed record ChangeQuoteRequest(
        long OrderId,
        int CommercialVersion,
        long ElectronicTicketId,
        long OrderServiceId,
        long TicketCouponId,
        int SaleCurrencyId);

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
