using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;

namespace AeroTech.Ordering.Domain.Ports.Exchange
{
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
}
