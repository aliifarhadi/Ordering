using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedOrderSource(
        string SourceSystem,
        string SourceOfferId,
        int SaleCurrencyId,
        DateTimeOffset? TicketingDeadline,
        IReadOnlyList<AcceptedSourceTraveller> Travellers,
        IReadOnlyList<AcceptedJourney> Journeys,
        IReadOnlyList<AcceptedProduct> Products,
        IReadOnlyList<AcceptedSourcePricingLine> PricingLines,
        IReadOnlyList<AcceptedFareConstruction>? FareConstructions = null);
}
