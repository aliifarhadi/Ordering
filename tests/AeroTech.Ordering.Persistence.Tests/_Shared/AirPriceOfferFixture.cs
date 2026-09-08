using AeroTech.Messages.AirPrice.Enums;
using AeroTech.Ordering.Providers.Offer.Model;

namespace AeroTech.Ordering.Persistence.Tests._Shared
{
    public static class AirPriceOfferFixture
    {
        public const int CurrencyId = 1;
        public const int ForeignCurrencyId = 8;
        public const long AirFareId = 900;
        public const long FlightId = 5001;
        public const string OfferId = "OFFER-1";

        public static OfferDetail Offer(
            DateTimeOffset now,
            IReadOnlyList<OfferPriceLine>? priceLines = null,
            IReadOnlyList<OfferCharge>? charges = null,
            IReadOnlyList<OfferRate>? rates = null,
            string? baggageUnit = "KG",
            IReadOnlyList<OfferPriceLine>? orderCharges = null)
        {
            var departure = now.AddDays(30);

            return new OfferDetail(
                OfferId: OfferId,
                CurrencyId: CurrencyId,
                LastTicketingDate: now.AddDays(1),
                Travellers: [new OfferTraveller("T1", 1, "ADT")],
                Bounds:
                [
                    new OfferBound("B1", 1, 100, 200,
                    [
                        new OfferFlight(
                            FlightId: FlightId,
                            FlightVersion: 1,
                            Number: "W5 1234",
                            OriginAirportId: 100,
                            OriginAirportTerminalId: null,
                            DestinationAirportId: 200,
                            DestinationAirportTerminalId: null,
                            OperatingAirlineId: 20,
                            MarketingAirlineId: 10,
                            DepartureDateTime: departure,
                            ArrivalDateTime: departure.AddHours(2),
                            Duration: 120,
                            AircraftId: 1,
                            CabinClassId: 1,
                            RbdId: 1,
                            BookingClass: "Y",
                            FlightCapacityId: 77,
                            Legs: [])
                    ])
                ],
                FareComponents:
                [
                    new OfferFareComponent(
                        AirFareId: AirFareId,
                        BoundId: "B1",
                        BookingClass: "Y",
                        FareBasis: "YOW",
                        FareFamily: "ECO",
                        IsRefundable: true,
                        IsChangeable: true,
                        IsUpgradable: false,
                        BaggagePieces: 1,
                        BaggageWeight: 20m,
                        BaggageUnit: baggageUnit,
                        CabinBaggagePieces: 1,
                        CabinBaggageWeight: 7m,
                        CabinBaggageUnit: "KG")
                ],
                PriceLines: priceLines ??
                [
                    FareLine(1_000_000m),
                    ChargeLine("TAX-1", "I6", 90_000m)
                ],
                OrderCharges: orderCharges ?? [],
                Charges: charges ?? [new OfferCharge("TAX-1", AirChargeKind.Tax, "I6", "Value added tax", true)],
                Rates: rates ?? []);
        }

        public const long SecondFlightId = 5002;

        public static OfferDetail TwoFlightOffer(
            DateTimeOffset now,
            IReadOnlyList<OfferPriceLine> priceLines,
            IReadOnlyList<OfferCharge>? charges = null)
        {
            var offer = Offer(now, priceLines, charges);
            var bound = offer.Bounds.Single();
            var first = bound.Flights.Single();

            var second = first with
            {
                FlightId = SecondFlightId,
                Number = "W5 5678",
                OriginAirportId = 200,
                DestinationAirportId = 300,
                DepartureDateTime = first.ArrivalDateTime.AddHours(2),
                ArrivalDateTime = first.ArrivalDateTime.AddHours(4)
            };

            return offer with { Bounds = [bound with { Flights = [first, second] }] };
        }

        public static OfferPriceLine FareLine(decimal amount)
            => new("T1", true, AirFareId, null, "FARE", "B1", FlightId, amount, CurrencyId, amount, CurrencyId, null);

        public static OfferPriceLine ItemFareLine(decimal amount)
            => new("T1", true, AirFareId, null, "FARE", "B1", null, amount, CurrencyId, amount, CurrencyId, null);

        public static OfferPriceLine ChargeLineOn(string airChargeId, string code, decimal amount, long flightId)
            => new("T1", false, AirFareId, airChargeId, code, "B1", flightId, amount, CurrencyId, amount, CurrencyId, null);

        public static OfferPriceLine ChargeLine(string airChargeId, string code, decimal amount)
            => new("T1", false, AirFareId, airChargeId, code, "B1", FlightId, amount, CurrencyId, amount, CurrencyId, null);

        public static OfferPriceLine ConvertedChargeLine(
            string airChargeId,
            string code,
            decimal originalAmount,
            decimal saleAmount,
            string rateId)
            => new("T1", false, AirFareId, airChargeId, code, "B1", FlightId, originalAmount, ForeignCurrencyId, saleAmount, CurrencyId, rateId);
    }
}
