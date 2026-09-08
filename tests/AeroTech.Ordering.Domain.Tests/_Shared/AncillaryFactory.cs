using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public static class AncillaryFactory
    {
        public const string AncillaryProductRef = "ANC-PRODUCT";
        public const string SourceSystem = "AirPrice";

        public static Order OrderWith(SequentialIdGenerator ids, TestClock clock, params AcceptedService[] services)
            => Order.Create(
                MultiPassengerOrderFactory.Args(),
                SourceWith(clock, services),
                MultiPassengerOrderFactory.OwnerAirlineId,
                ids,
                clock);

        public static AcceptedOrderSource SourceWith(TestClock clock, params AcceptedService[] services)
        {
            var source = MultiPassengerOrderFactory.AcceptedSource(clock);

            if (services.Length == 0)
                return source;

            return source with
            {
                Products = [.. source.Products, AncillaryProduct(services)]
            };
        }

        public static AcceptedOrderSource MapAirDetails(
            AcceptedOrderSource source,
            Func<AcceptedAirTransportDetail, AcceptedAirTransportDetail> map)
            => source with
            {
                Products = source.Products
                    .Select(product => product with
                    {
                        Services = product.Services
                            .Select(service => service.Detail is AcceptedAirTransportDetail air
                                ? service with { Detail = map(air) }
                                : service)
                            .ToList()
                    })
                    .ToList()
            };

        public static AcceptedProduct AncillaryProduct(params AcceptedService[] services)
            => new(
                ProductRef: AncillaryProductRef,
                TravellerRef: "T1",
                ProductType: ProductType.Baggage,
                Quantity: 1m,
                UnitOfMeasure: OrderItemUnitOfMeasure.Each,
                Snapshot: new AcceptedProductSnapshot(
                    ProductType.Baggage,
                    "ANC-1",
                    SourceSystem,
                    MultiPassengerOrderFactory.SourceOfferId),
                CommercialTerms: new AcceptedCommercialTerms(
                    CommercialTermState.Unknown,
                    CommercialTermState.Unknown,
                    CommercialTermState.Unknown,
                    SourceSystem),
                Services: services);

        public static AcceptedService Service(
            string serviceRef,
            OrderServiceType serviceType,
            string serviceCode,
            AcceptedServiceDetail detail,
            IReadOnlyList<string> beneficiaries,
            ServicePriceTreatment priceTreatment = ServicePriceTreatment.SeparatelyPriced,
            bool requiresReservation = false,
            bool requiresDocument = false,
            ServiceDocumentKind? documentKind = null,
            IReadOnlyList<string>? coveredAirServiceRefs = null,
            IReadOnlyList<string>? coveredSegmentRefs = null)
            => new(
                ServiceRef: serviceRef,
                ServiceType: serviceType,
                ServiceCode: serviceCode,
                Name: serviceCode,
                DeliveryModel: DeliveryModel.PerPassengerSegment,
                PriceTreatment: priceTreatment,
                RequiresReservation: requiresReservation,
                RequiresSupplierConfirmation: false,
                RequiresDocument: requiresDocument,
                ProviderType: OrderProviderType.Airline,
                BeneficiaryTravellerRefs: beneficiaries,
                Detail: detail,
                DocumentKind: documentKind,
                CoveredAirServiceRefs: coveredAirServiceRefs,
                CoveredSegmentRefs: coveredSegmentRefs);

        public static string AirServiceRef(string travellerRef, string journeyRef, long flightId)
            => $"{travellerRef}:{journeyRef}:{flightId}";

        public static string OutboundAirServiceRef(string travellerRef = "T1")
            => AirServiceRef(travellerRef, "B1", MultiPassengerOrderFactory.OutboundFlightId);

        public static string InboundAirServiceRef(string travellerRef = "T1")
            => AirServiceRef(travellerRef, "B2", MultiPassengerOrderFactory.InboundFlightId);

        public static string OutboundSegmentRef() => $"B1:{MultiPassengerOrderFactory.OutboundFlightId}";

        public static AcceptedService Seat(string serviceRef = "SEAT-1", string? seatNumber = "12A")
            => Service(
                serviceRef,
                OrderServiceType.SeatAssignment,
                "SEAT",
                new AcceptedSeatDetail(OutboundAirServiceRef(), seatNumber),
                ["T1"],
                coveredAirServiceRefs: [OutboundAirServiceRef()]);

        public static AcceptedService Baggage(
            string serviceRef = "BAG-1",
            BaggageServiceKind kind = BaggageServiceKind.PrepaidPiece,
            int? pieces = 1,
            decimal? weight = null,
            BaggageWeightUnit? unit = null,
            ServicePriceTreatment priceTreatment = ServicePriceTreatment.SeparatelyPriced,
            IReadOnlyList<string>? beneficiaries = null,
            IReadOnlyList<string>? coveredAirServiceRefs = null,
            decimal? perPieceWeightLimit = null)
            => Service(
                serviceRef,
                OrderServiceType.BaggageAllowance,
                "BAG",
                new AcceptedBaggageDetail(kind, pieces, weight, unit, perPieceWeightLimit),
                beneficiaries ?? ["T1"],
                priceTreatment,
                coveredAirServiceRefs: coveredAirServiceRefs ?? [OutboundAirServiceRef()]);

        public static AcceptedService Meal(
            string serviceRef = "MEAL-1",
            int quantity = 1,
            ServicePriceTreatment priceTreatment = ServicePriceTreatment.SeparatelyPriced)
            => Service(
                serviceRef,
                OrderServiceType.Meal,
                "MEAL",
                new AcceptedMealDetail("VGML", quantity, "VG"),
                ["T1"],
                priceTreatment,
                coveredAirServiceRefs: [OutboundAirServiceRef()]);

        public static AcceptedService Lounge(
            string serviceRef = "LNG-1",
            int airportId = 100,
            int guestCount = 0,
            string? relatedAirServiceRef = null,
            DateTimeOffset? accessStart = null,
            DateTimeOffset? accessEnd = null)
            => Service(
                serviceRef,
                OrderServiceType.LoungeAccess,
                "LNG",
                new AcceptedLoungeDetail(airportId, guestCount, "LNG-A", accessStart, accessEnd, relatedAirServiceRef),
                ["T1"]);

        public static AcceptedService Hotel(
            string serviceRef = "HTL-1",
            int roomCount = 1,
            int guestCount = 2,
            int nights = 3,
            IReadOnlyList<string>? beneficiaries = null)
            => Service(
                serviceRef,
                OrderServiceType.HotelStay,
                "HTL",
                new AcceptedHotelDetail(
                    "PROP-1",
                    new DateOnly(2026, 10, 1),
                    new DateOnly(2026, 10, 1).AddDays(nights),
                    roomCount,
                    guestCount,
                    "SUP-1",
                    "DBL",
                    "RATE-1"),
                beneficiaries ?? ["T1", "T2"]);

        public static AcceptedService GroundTransport(
            string serviceRef = "GRD-1",
            int passengerCount = 3,
            IReadOnlyList<string>? beneficiaries = null)
            => Service(
                serviceRef,
                OrderServiceType.GroundTransport,
                "GRD",
                new AcceptedGroundTransportDetail("LOC-A", "LOC-B", new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), passengerCount, "VAN"),
                beneficiaries ?? ["T1", "T2"]);

        public static AcceptedService Generic(
            string serviceRef,
            OrderServiceType serviceType,
            string schemaName,
            string schemaVersion,
            string attributesJson,
            IReadOnlyList<string>? beneficiaries = null,
            IReadOnlyList<string>? coveredAirServiceRefs = null)
            => Service(
                serviceRef,
                serviceType,
                schemaName.ToUpperInvariant(),
                new AcceptedGenericServiceDetail(schemaName, schemaVersion, attributesJson),
                beneficiaries ?? ["T1"],
                coveredAirServiceRefs: coveredAirServiceRefs);

        public static AcceptedService Priority(string serviceRef = "PRI-1")
            => Generic(serviceRef, OrderServiceType.Priority, "Priority", "1.0", """{"priorityKind":"Boarding"}""");

        public static AcceptedService WiFi(string serviceRef = "WIFI-1")
            => Generic(serviceRef, OrderServiceType.WiFi, "WiFi", "1.0", """{"accessKind":"FullFlight"}""",
                coveredAirServiceRefs: [OutboundAirServiceRef()]);

        public static AcceptedService SimCard(string serviceRef = "SIM-1")
            => Generic(serviceRef, OrderServiceType.SimCard, "SimCard", "1.0", """{"planCode":"EU-5GB"}""");

        public static AcceptedService ExtraSeat(string serviceRef = "EXST-1")
            => Generic(
                serviceRef,
                OrderServiceType.ExtraSeat,
                "ExtraSeat",
                "1.0",
                """{"capacityQuantity":1,"reason":"CBBG"}""",
                coveredAirServiceRefs: [OutboundAirServiceRef()]);
    }
}
