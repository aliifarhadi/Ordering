using AeroTech.Messages.AirPrice.Enums;

namespace AeroTech.Ordering.Providers.Offer.Wire
{
    public sealed class OfferEnvelope<T>
    {
        public T? Data { get; set; }

        public OfferResultError[]? Errors { get; set; }
    }

    public sealed class OfferResultError
    {
        public int Code { get; set; }

        public string? Title { get; set; }

        public string? Detail { get; set; }
    }

    public sealed class FlightOfferDetailResponse
    {
        public string OfferId { get; set; } = string.Empty;

        public DateTimeOffset? LastTicketingDate { get; set; }

        public int CurrencyId { get; set; }

        public List<OfferAirTransport> AirTransports { get; set; } = new();

        public List<OfferPricingUnit> PricingUnits { get; set; } = new();

        public List<OfferTicket> Tickets { get; set; } = new();

        public List<OfferPricingLine> OrderCharges { get; set; } = new();

        public List<OfferRateOfExchange> RatesOfExchange { get; set; } = new();
    }

    public sealed class OfferAirTransport
    {
        public string BoundId { get; set; } = string.Empty;

        public int Sequence { get; set; }

        public long OriginAirportId { get; set; }

        public long DestinationAirportId { get; set; }

        public List<OfferFlight> Flights { get; set; } = new();
    }

    public sealed class OfferFlight
    {
        public long FlightId { get; set; }

        public int FlightVersion { get; set; }

        public string? FlightNumber { get; set; }

        public long OriginAirportId { get; set; }

        public long? OriginAirportTerminalId { get; set; }

        public long DestinationAirportId { get; set; }

        public long? DestinationAirportTerminalId { get; set; }

        public long OperatingAirlineId { get; set; }

        public long MarketingAirlineId { get; set; }

        public DateTimeOffset DepartureDateTime { get; set; }

        public DateTimeOffset ArrivalDateTime { get; set; }

        public int Duration { get; set; }

        public long? AircraftId { get; set; }

        public long? CabinClassId { get; set; }

        public long? RbdId { get; set; }

        public string? BookingClass { get; set; }

        public long FlightCapacityId { get; set; }

        public List<OfferFlightLeg> Legs { get; set; } = new();
    }

    public sealed class OfferFlightLeg
    {
        public long LegId { get; set; }

        public int Sequence { get; set; }

        public long OriginAirportId { get; set; }

        public long? OriginAirportTerminalId { get; set; }

        public long DestinationAirportId { get; set; }

        public long? DestinationAirportTerminalId { get; set; }

        public DateTimeOffset DepartureDateTime { get; set; }

        public DateTimeOffset ArrivalDateTime { get; set; }
        public OfferFlightStop? Stop { get; set; }
}

    public class OfferFlightStop
    {
        public int DurationMinutes { get; set; }
        public StopType StopType { get; set; }
        public bool PassengersCanBoardOrLeave { get; set; }
    }

    public sealed class OfferTicket
    {
        public string TravellerRef { get; set; } = string.Empty;

        public int TravellerIndex { get; set; }

        public string PassengerTypeCode { get; set; } = string.Empty;

        public List<OfferCoupon> Coupons { get; set; } = new();
    }

    public sealed class OfferCoupon
    {
        public string BoundId { get; set; } = string.Empty;

        public long FlightId { get; set; }

        public int BaggagePieces { get; set; }

        public decimal BaggageWeight { get; set; }

        public string? BaggageUnit { get; set; }

        public int CabinBaggagePieces { get; set; }

        public decimal CabinBaggageWeight { get; set; }

        public string? CabinBaggageUnit { get; set; }

        public bool IsRefundable { get; set; }

        public bool IsChangeable { get; set; }

        public bool IsUpgradable { get; set; }

        public List<OfferPricingLine> Pricings { get; set; } = new();
    }

    public sealed class OfferPricingUnit
    {
        public string Kind { get; set; } = string.Empty;

        public List<string> CoveredBoundOfferIds { get; set; } = new();

        public List<OfferFareComponent> FareComponents { get; set; } = new();
    }

    public sealed class OfferFareComponent
    {
        public long AirFareId { get; set; }

        public string? BookingClass { get; set; }

        public string? FareBasis { get; set; }

        public string? FareFamily { get; set; }

        public string? FareType { get; set; }
    }

    public sealed class OfferPricingLine
    {
        public OfferPricingCategory Category { get; set; }

        public string? Name { get; set; }

        public string? Code { get; set; }

        public string? Reference { get; set; }

        public decimal Amount { get; set; }

        public int CurrencyId { get; set; }

        public decimal EquivalentAmount { get; set; }

        public int EquivalentCurrencyId { get; set; }

        public string? RateOfExchangePeriodId { get; set; }
    }

    public sealed class OfferRateOfExchange
    {
        public string RateOfExchangePeriodId { get; set; } = string.Empty;

        public int FromCurrencyId { get; set; }

        public int ToCurrencyId { get; set; }

        public decimal Rate { get; set; }

        public int DecimalPlaces { get; set; }

        public float RoundingFactor { get; set; }
    }

    public enum OfferPricingCategory
    {
        Fare = 0,
        Tax = 1,
        Fee = 2,
        Surcharge = 3
    }
}
