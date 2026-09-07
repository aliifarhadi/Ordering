using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Offers
{
    public sealed record OfferDetail(
        string OfferId,
        int CurrencyId,
        DateTimeOffset? LastTicketingDate,
        IReadOnlyList<OfferTraveller> Travellers,
        IReadOnlyList<OfferBound> Bounds,
        IReadOnlyList<OfferFareComponent> FareComponents,
        IReadOnlyList<OfferPriceLine> PriceLines,
        IReadOnlyList<OfferPriceLine> OrderCharges,
        IReadOnlyList<OfferCharge> Charges,
        IReadOnlyList<OfferRate> Rates);

    public sealed record OfferTraveller(string TravellerRef, int TravellerIndex, string PassengerTypeCode);

    public sealed record OfferBound(
        string BoundId,
        int Sequence,
        long OriginAirportId,
        long DestinationAirportId,
        IReadOnlyList<OfferFlight> Flights);

    public sealed record OfferFlight(
        long FlightId,
        int FlightVersion,
        string Number,
        long OriginAirportId,
        long? OriginAirportTerminalId,
        long DestinationAirportId,
        long? DestinationAirportTerminalId,
        long OperatingAirlineId,
        long MarketingAirlineId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime,
        int Duration,
        long? AircraftId,
        long? CabinClassId,
        long? RbdId,
        string? BookingClass,
        long FlightCapacityId,
        IReadOnlyList<OfferFlightLeg> Legs);

    public sealed record OfferFlightLeg(
        long LegId,
        int Sequence,
        long OriginAirportId,
        long? OriginAirportTerminalId,
        long DestinationAirportId,
        long? DestinationAirportTerminalId,
        DateTimeOffset DepartureDateTime,
        DateTimeOffset ArrivalDateTime,
        OfferFlightStop? Stop);

    public sealed record OfferFlightStop(
        int DurationMinutes,
        StopType StopType,
        bool PassengersCanBoardOrLeave);



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

    public sealed record OfferCharge(
        string AirChargeId,
        AirChargeKind Kind,
        string? Code,
        string? Name,
        bool IsRefundable);

    public sealed record OfferRate(
        string RateOfExchangePeriodId,
        int FromCurrencyId,
        int ToCurrencyId,
        decimal Rate,
        int DecimalPlaces);
}
