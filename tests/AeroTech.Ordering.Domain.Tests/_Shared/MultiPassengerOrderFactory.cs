using AeroTech.Messages.AirPrice.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Offers;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public static class MultiPassengerOrderFactory
    {
        public const int CurrencyId = 1;
        public const long OutboundAirFareId = 900;
        public const long InboundAirFareId = 901;
        public const long OutboundFlightId = 5001;
        public const long InboundFlightId = 5002;
        public const long OwnerAirlineId = 77;

        public static Order Create(SequentialIdGenerator ids, TestClock clock)
            => Order.Create(Args(), Offer(clock), OwnerAirlineId, ids, clock);

        public static Order CreateWithThroughFare(SequentialIdGenerator ids, TestClock clock)
            => Order.Create(Args(), Offer(clock, throughFare: true), OwnerAirlineId, ids, clock);

        public static CreateOrderArgs Args() => new(
            CustomerId: 42,
            Channel: SalesChannel.BackOffice,
            CreatorUserId: 7,
            AirlineOfficeId: 11,
            OfferId: "OFFER-RT-2PAX",
            CommissionRate: 0m,
            Travellers:
            [
                Traveller(1, "ALI", "FARHADI"),
                Traveller(2, "SARA", "FARHADI")
            ],
            Contact: new CreateOrderContactArgs(
                "ALI FARHADI",
                [new CreateContactPointArgs(ContactPointType.Email, "a@b.c", null, true)]),
            SeatSelections: []);

        private static CreateOrderTravellerArgs Traveller(int index, string firstName, string surname) => new(
            Index: index,
            ParentIndex: null,
            FirstName: firstName,
            SurName: surname,
            NoSurname: false,
            PassengerType: PassengerTypeCode.ADT,
            AgeRange: AgeRange.Adult,
            DateOfBirth: new DateOnly(1990, 1, index),
            Gender: index == 1 ? Gender.Male : Gender.Female,
            NationalityId: 1,
            CountryOfResidenceId: 1,
            Documents: []);

        public static OfferDetail Offer(TestClock clock, bool throughFare = false)
        {
            var inboundFareId = throughFare ? OutboundAirFareId : InboundAirFareId;
            var outbound = clock.GetDateTime().AddDays(30);
            var inbound = clock.GetDateTime().AddDays(37);

            return new OfferDetail(
                OfferId: "OFFER-RT-2PAX",
                CurrencyId: CurrencyId,
                LastTicketingDate: clock.GetDateTime().AddDays(1),
                Travellers:
                [
                    new OfferTraveller("T1", 1, "ADT"),
                    new OfferTraveller("T2", 2, "ADT")
                ],
                Bounds:
                [
                    Bound("B1", 1, 100, 200, OutboundFlightId, "W5 1234", outbound),
                    Bound("B2", 2, 200, 100, InboundFlightId, "W5 4321", inbound)
                ],
                FareComponents:
                [
                    FareComponent(OutboundAirFareId, "B1"),
                    FareComponent(inboundFareId, "B2")
                ],
                PriceLines:
                [
                    Line("T1", true, OutboundAirFareId, null, "FARE", "B1", OutboundFlightId, 1_000_000m),
                    Line("T1", false, OutboundAirFareId, "TAX-1", "I6", "B1", OutboundFlightId, 90_000m),
                    Line("T1", true, inboundFareId, null, "FARE", "B2", InboundFlightId, 1_100_000m),
                    Line("T1", false, inboundFareId, "TAX-1", "I6", "B2", InboundFlightId, 99_000m),
                    Line("T2", true, OutboundAirFareId, null, "FARE", "B1", OutboundFlightId, 1_000_000m),
                    Line("T2", false, OutboundAirFareId, "TAX-1", "I6", "B1", OutboundFlightId, 90_000m),
                    Line("T2", true, inboundFareId, null, "FARE", "B2", InboundFlightId, 1_100_000m),
                    Line("T2", false, inboundFareId, "TAX-1", "I6", "B2", InboundFlightId, 99_000m)
                ],
                OrderCharges: [],
                Charges: [new OfferCharge("TAX-1", AirChargeKind.Tax, "I6", "Value added tax", true)],
                Rates: []);
        }

        private static OfferBound Bound(
            string boundId,
            int sequence,
            long origin,
            long destination,
            long flightId,
            string number,
            DateTimeOffset departure) => new(
            BoundId: boundId,
            Sequence: sequence,
            OriginAirportId: origin,
            DestinationAirportId: destination,
            Flights:
            [
                new OfferFlight(
                    FlightId: flightId,
                    FlightVersion: 1,
                    Number: number,
                    OriginAirportId: origin,
                    OriginAirportTerminalId: null,
                    DestinationAirportId: destination,
                    DestinationAirportTerminalId: null,
                    OperatingAirlineId: 10,
                    MarketingAirlineId: 10,
                    DepartureDateTime: departure,
                    ArrivalDateTime: departure.AddHours(2),
                    Duration: 120,
                    AircraftId: 1,
                    CabinClassId: 1,
                    RbdId: 1,
                    BookingClass: "Y",
                    FlightCapacityId: 70 + flightId,
                    Legs: [])
            ]);

        private static OfferFareComponent FareComponent(long airFareId, string boundId) => new(
            AirFareId: airFareId,
            BoundId: boundId,
            BookingClass: "Y",
            FareBasis: "YRT",
            FareFamily: "ECO",
            IsRefundable: true,
            IsChangeable: true,
            IsUpgradable: false,
            BaggagePieces: 1,
            BaggageWeight: 20m,
            BaggageUnit: "KG",
            CabinBaggagePieces: 1,
            CabinBaggageWeight: 7m,
            CabinBaggageUnit: "KG");

        private static OfferPriceLine Line(
            string travellerRef,
            bool isBase,
            long airFareId,
            string? airChargeId,
            string code,
            string boundId,
            long flightId,
            decimal amount)
            => new(travellerRef, isBase, airFareId, airChargeId, code, boundId, flightId, amount, CurrencyId, amount, CurrencyId, null);
    }
}
