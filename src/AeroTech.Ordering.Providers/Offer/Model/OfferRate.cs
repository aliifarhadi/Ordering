namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferRate(
        string RateOfExchangePeriodId,
        int FromCurrencyId,
        int ToCurrencyId,
        decimal Rate,
        int DecimalPlaces);
}
