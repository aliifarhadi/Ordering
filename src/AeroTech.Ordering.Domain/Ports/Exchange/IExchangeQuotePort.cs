using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;

namespace AeroTech.Ordering.Domain.Ports.Exchange
{
    public interface IExchangeQuotePort
    {
        Task<ExchangeQuote> QuoteAsync(ExchangeQuoteRequest request, CancellationToken cancellationToken = default);

        Task<AcceptedExchange> AcceptQuotedExchangeAsync(
            AcceptedQuotedExchangeSelection selection,
            CancellationToken cancellationToken = default);
    }

    public sealed record ExchangeQuoteRequest(
        long OrderId,
        int CommercialVersion,
        long PredecessorElectronicTicketId,
        string PredecessorDocumentNumber,
        IReadOnlyList<long> ChangedOrderServiceIds,
        IReadOnlyList<ExchangeScopeCoupon> ExchangeScope,
        IReadOnlyList<HistoricalUsedCoupon> HistoricalUsedCoupons,
        IReadOnlyList<PredecessorPricingEvidence> PredecessorPricing,
        IReadOnlyList<FareConstructionContext> FareConstructions,
        int SaleCurrencyId);

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
