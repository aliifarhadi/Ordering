using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using BoundDirection = AeroTech.Messages.Ordering.Enums.BoundDirection;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

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
        public const string SourceOfferId = "OFFER-RT-2PAX";
        public const string SourceSystem = "AirPrice";
        public const string FareBasis = "YRT";

        private const decimal OutboundFare = 1_000_000m;
        private const decimal OutboundTax = 90_000m;
        private const decimal InboundFare = 1_100_000m;
        private const decimal InboundTax = 99_000m;

        public static Order Create(SequentialIdGenerator ids, TestClock clock)
            => Order.Create(Args(), AcceptedSource(clock), OwnerAirlineId, ids, clock);

        public static Order CreateWithThroughFare(SequentialIdGenerator ids, TestClock clock)
            => Order.Create(Args(), AcceptedSource(clock, throughFare: true), OwnerAirlineId, ids, clock);

        public static CreateOrderArgs Args() => new(
            CustomerId: 42,
            Channel: SalesChannel.BackOffice,
            CreatorUserId: 7,
            AirlineOfficeId: 11,
            OfferId: SourceOfferId,
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

        public static AcceptedOrderSource AcceptedSource(TestClock clock, bool throughFare = false)
        {
            var inboundFareId = throughFare ? OutboundAirFareId : InboundAirFareId;
            var outbound = clock.GetDateTime().AddDays(30);
            var inbound = clock.GetDateTime().AddDays(37);

            var journeys = new[]
            {
                Journey("B1", 1, 100, 200, OutboundFlightId, "W5 1234", outbound, BoundDirection.Outbound, OutboundAirFareId),
                Journey("B2", 2, 200, 100, InboundFlightId, "W5 4321", inbound, BoundDirection.Inbound, inboundFareId)
            };

            var products = new List<AcceptedProduct>();
            var pricingLines = new List<AcceptedSourcePricingLine>();

            foreach (var travellerRef in new[] { "T1", "T2" })
            {
                var byProduct = new Dictionary<string, List<AcceptedService>>(StringComparer.OrdinalIgnoreCase);

                foreach (var leg in new[]
                         {
                             ("B1", OutboundFlightId, OutboundAirFareId, OutboundFare, OutboundTax),
                             ("B2", InboundFlightId, inboundFareId, InboundFare, InboundTax)
                         })
                {
                    var (journeyRef, flightId, fareId, fare, tax) = leg;
                    var productRef = ProductRef(travellerRef, fareId);
                    var serviceRef = ServiceRef(travellerRef, journeyRef, flightId);

                    if (!byProduct.TryGetValue(productRef, out var services))
                    {
                        services = [];
                        byProduct.Add(productRef, services);
                    }

                    services.Add(Service(serviceRef, travellerRef, SegmentRef(journeyRef, flightId), fareId));

                    pricingLines.Add(Line(
                        PricingComponentType.Fare,
                        fare,
                        "YRT",
                        null,
                        fareId.ToString(),
                        productRef,
                        serviceRef,
                        travellerRef,
                        journeyRef,
                        flightId));

                    pricingLines.Add(Line(
                        PricingComponentType.Tax,
                        tax,
                        "I6",
                        "Value added tax",
                        "TAX-1",
                        productRef,
                        serviceRef,
                        travellerRef,
                        journeyRef,
                        flightId));
                }

                products.AddRange(byProduct.Select(entry => Product(entry.Key, travellerRef, FareIdOf(entry.Key), entry.Value)));
            }

            return new AcceptedOrderSource(
                SourceSystem: SourceSystem,
                SourceOfferId: SourceOfferId,
                SaleCurrencyId: CurrencyId,
                TicketingDeadline: clock.GetDateTime().AddDays(1),
                Travellers: [new AcceptedSourceTraveller("T1", 1), new AcceptedSourceTraveller("T2", 2)],
                Journeys: journeys,
                Products: products,
                PricingLines: pricingLines);
        }

        private static AcceptedJourney Journey(
            string journeyRef,
            int sequence,
            long origin,
            long destination,
            long flightId,
            string number,
            DateTimeOffset departure,
            BoundDirection direction,
            long fareId)
            => new(
                journeyRef,
                sequence,
                origin,
                destination,
                direction,
                [
                    new AcceptedSegment(
                        SegmentRef(journeyRef, flightId),
                        1,
                        flightId,
                        1,
                        number,
                        (int)origin,
                        null,
                        (int)destination,
                        null,
                        10,
                        10,
                        departure,
                        departure.AddHours(2),
                        120,
                        1,
                        1,
                        1,
                        "Y",
                        "Y",
                        70 + flightId,
                        fareId,
                        [])
                ]);

        private static AcceptedService Service(string serviceRef, string travellerRef, string segmentRef, long fareId)
            => new(
                serviceRef,
                travellerRef,
                segmentRef,
                OrderServiceType.AirTransportation,
                "AIR",
                "Air transportation",
                DeliveryModel.PerPassengerSegment,
                RequiresFulfillment: true,
                RequiresSupplierConfirmation: false,
                RequiresDocument: true,
                OrderProviderType.Airline,
                SupplierCode: null,
                new AcceptedAirServiceDetail(
                    fareId,
                    FareBasis,
                    "ECO",
                    CheckedBaggage(),
                    CabinBaggage()));

        private static AcceptedProduct Product(
            string productRef,
            string travellerRef,
            long fareId,
            IReadOnlyList<AcceptedService> services)
            => new(
                productRef,
                travellerRef,
                ProductType.AirFare,
                1m,
                OrderItemUnitOfMeasure.PassengerFare,
                new AcceptedProductSnapshot(
                    ProductType.AirFare,
                    fareId.ToString(),
                    SourceSystem,
                    SourceOfferId,
                    ProductCode: null,
                    ProductName: null,
                    BrandCode: null,
                    BrandName: "ECO",
                    MarketingAirlineId: 10,
                    OperatingAirlineId: 10,
                    SupplierCode: null,
                    SourcePricingReference: null),
                new AcceptedCommercialTerms(
                    CommercialTermState.Permitted,
                    CommercialTermState.Permitted,
                    CommercialTermState.Prohibited,
                    SourceSystem,
                    SourcePolicyReference: null),
                services);

        private static AcceptedSourcePricingLine Line(
            PricingComponentType componentType,
            decimal amount,
            string? code,
            string? description,
            string sourceCode,
            string productRef,
            string serviceRef,
            string travellerRef,
            string journeyRef,
            long flightId)
            => new(
                componentType,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                amount,
                CurrencyId,
                amount,
                CurrencyId,
                PricingBasisType.OrderService,
                PricingApplicationLevel.PerSegment,
                RefundabilityRule.Refundable,
                $"{SourceOfferId}:{travellerRef}:{journeyRef}:{flightId}:{sourceCode}",
                "1",
                productRef,
                serviceRef,
                code,
                description,
                null);

        private static AcceptedBaggageAllowance CheckedBaggage() => new(1, 20m, BaggageWeightUnit.Kg);

        private static AcceptedBaggageAllowance CabinBaggage() => new(1, 7m, BaggageWeightUnit.Kg);

        private static string SegmentRef(string journeyRef, long flightId) => $"{journeyRef}:{flightId}";

        private static string ServiceRef(string travellerRef, string journeyRef, long flightId)
            => $"{travellerRef}:{journeyRef}:{flightId}";

        private static string ProductRef(string travellerRef, long fareId) => $"{travellerRef}:{fareId}:{FareBasis}";

        private static long FareIdOf(string productRef) => long.Parse(productRef.Split(':')[1]);
    }
}
