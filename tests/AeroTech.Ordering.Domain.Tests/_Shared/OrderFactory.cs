using AeroTech.Messages.AirPrice.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Offers;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public static class OrderFactory
    {
        public const int CurrencyId = 1;
        public const long AirFareId = 900;
        public const long FlightId = 5001;

        public static Order CreatedOrder(SequentialIdGenerator ids, TestClock clock)
            => Order.Create(Args(), Offer(clock), ids, clock);

        public static CreateOrderArgs Args() => new(
            CustomerId: 42,
            Channel: SalesChannel.BackOffice,
            CreatorUserId: 7,
            AirlineOfficeId: 11,
            OfferId: "OFFER-1",
            CommissionRate: 0m,
            Travellers: new[]
            {
                new CreateOrderTravellerArgs(
                    Index: 1,
                    ParentIndex: null,
                    FirstName: "ALI",
                    SurName: "FARHADI",
                    NoSurname: false,
                    PassengerType: PassengerTypeCode.ADT,
                    AgeRange: AgeRange.Adult,
                    DateOfBirth: new DateOnly(1990, 1, 1),
                    Gender: Gender.Male,
                    NationalityId: 1,
                    CountryOfResidenceId: 1,
                    Documents: Array.Empty<CreateTravellerDocumentArgs>())
            },
            Contact: new CreateOrderContactArgs(
                "ALI FARHADI",
                new[] { new CreateContactPointArgs(ContactPointType.Email, "a@b.c", null, true) }),
            SeatSelections: Array.Empty<CreateOrderSeatSelectionArgs>());

        public static OfferDetail Offer(TestClock clock)
        {
            var departure = clock.GetDateTime().AddDays(30);

            return new OfferDetail(
                OfferId: "OFFER-1",
                CurrencyId: CurrencyId,
                LastTicketingDate: clock.GetDateTime().AddDays(1),
                Travellers: new[] { new OfferTraveller("T1", 1, "ADT") },
                Bounds: new[]
                {
                    new OfferBound(
                        BoundId: "B1",
                        Sequence: 1,
                        OriginAirportId: 100,
                        DestinationAirportId: 200,
                        Flights: new[]
                        {
                            new OfferFlight(
                                FlightId: FlightId,
                                FlightVersion: 1,
                                Number: "W5 1234",
                                OriginAirportId: 100,
                                OriginAirportTerminalId: null,
                                DestinationAirportId: 200,
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
                                FlightCapacityId: 77,
                                Legs: Array.Empty<OfferFlightLeg>())
                        })
                },
                FareComponents: new[]
                {
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
                        BaggageUnit: "KG",
                        CabinBaggagePieces: 1,
                        CabinBaggageWeight: 7m,
                        CabinBaggageUnit: "KG")
                },
                PriceLines: new[]
                {
                    new OfferPriceLine("T1", true, AirFareId, null, "FARE", "B1", FlightId, 1_000_000m, CurrencyId, 1_000_000m, CurrencyId, null),
                    new OfferPriceLine("T1", false, AirFareId, "TAX-1", "I6", "B1", FlightId, 90_000m, CurrencyId, 90_000m, CurrencyId, null)
                },
                OrderCharges: Array.Empty<OfferPriceLine>(),
                Charges: new[] { new OfferCharge("TAX-1", AirChargeKind.Tax, "I6", "Value added tax", true) },
                Rates: Array.Empty<OfferRate>());
        }
    }
}
