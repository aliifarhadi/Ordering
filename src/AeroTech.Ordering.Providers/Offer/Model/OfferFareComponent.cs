namespace AeroTech.Ordering.Providers.Offer.Model
{
    public sealed record OfferFareComponent(
        long AirFareId,
        string BoundId,
        string? BookingClass,
        string? FareBasis,
        string? FareFamily,
        bool IsRefundable,
        bool IsChangeable,
        bool IsUpgradable,
        int BaggagePieces,
        decimal BaggageWeight,
        string? BaggageUnit,
        int CabinBaggagePieces,
        decimal CabinBaggageWeight,
        string? CabinBaggageUnit);
}
