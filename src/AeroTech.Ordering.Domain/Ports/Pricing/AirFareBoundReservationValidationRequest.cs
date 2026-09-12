namespace AeroTech.Ordering.Domain.Ports.Pricing
{
    public sealed record AirFareBoundReservationValidationRequest(
        AirFareBoundReservationSalesContext SalesContext,
        IReadOnlyList<AirFareBoundReservationPassenger> Passengers,
        IReadOnlyList<AirFareBoundReservationPricingUnit> PricingUnits);
}
