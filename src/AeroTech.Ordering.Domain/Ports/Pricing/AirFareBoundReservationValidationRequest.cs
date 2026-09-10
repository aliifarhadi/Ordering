using AeroTech.Messages.FlightFlow.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.Providers.Pricing
{
    public sealed record AirFareBoundReservationValidationRequest(
        AirFareBoundReservationSalesContext SalesContext,
        IReadOnlyList<AirFareBoundReservationPassenger> Passengers,
        IReadOnlyList<AirFareBoundReservationPricingUnit> PricingUnits);

    public sealed record AirFareBoundReservationSalesContext(
        long CreatorUserId,
        int? CountryId,
        SalesChannel? Channel,
        long CustomerId,
        DateTimeOffset SalesDate,
        int PreferredCurrencyId);

    public sealed record AirFareBoundReservationPassenger(
        string PassengerId,
        PassengerTypeCode PassengerTypeCode);

    public sealed record AirFareBoundReservationPricingUnit(
        string PricingUnitId,
        JourneyType JourneyType,
        IReadOnlyList<string> BoundIds,
        int PricingOriginAirportId,
        int PricingDestinationAirportId,
        IReadOnlyList<AirFareBoundReservationAirFare> AirFares);

    public sealed record AirFareBoundReservationAirFare(
        string AirFareId,
        IReadOnlyList<AirFareBoundReservationFlight> Flights);

    public sealed record AirFareBoundReservationFlight(
        string FlightId,
        string FlightNumber,
        int FlightOriginAirportId,
        int FlightDestinationAirportId,
        int AircraftId,
        DateTimeOffset DepartureDateTime,
        FlightStatus FlightStatus,
        DateTimeOffset? FlightStopBookDateTime,
        long RbdId,
        int RequiredSeats);
}
