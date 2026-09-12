using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.Pricing
{
    public sealed record AirFareBoundReservationPricingUnit(
        string PricingUnitId,
        JourneyType JourneyType,
        IReadOnlyList<string> BoundIds,
        int PricingOriginAirportId,
        int PricingDestinationAirportId,
        IReadOnlyList<AirFareBoundReservationAirFare> AirFares);
}
