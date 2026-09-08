namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferPriceLine(
        string TravellerRef,
        bool IsBase,
        long? AirFareId,
        string? AirChargeId,
        string? Code,
        string? BoundId,
        long? FlightId,
        decimal Amount,
        int? CurrencyId,
        decimal EquivalentAmount,
        int? EquivalentCurrencyId,
        string? RateOfExchangePeriodId);
}
