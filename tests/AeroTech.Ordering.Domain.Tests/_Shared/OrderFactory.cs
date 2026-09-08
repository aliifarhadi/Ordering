using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using BoundDirection = AeroTech.Messages.Ordering.Enums.BoundDirection;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public static class OrderFactory
    {
        public const int CurrencyId = 1;
        public const long AirFareId = 900;
        public const long FlightId = 5001;
        public const long OwnerAirlineId = 77;
        public const string SourceOfferId = "OFFER-1";
        public const string SourceSystem = "AirPrice";

        public const string JourneyRef = "B1";
        public const string TravellerRef = "T1";
        public const string SegmentRef = "B1:5001";
        public const string ServiceRef = "T1:B1:5001";
        public const string ProductRef = "T1:900:YOW";

        public static Order CreatedOrder(SequentialIdGenerator ids, TestClock clock)
            => Order.Create(Args(), AcceptedSource(clock), OwnerAirlineId, ids, clock);

        public static CreateOrderArgs Args() => new(
            CustomerId: 42,
            Channel: SalesChannel.BackOffice,
            CreatorUserId: 7,
            AirlineOfficeId: 11,
            OfferId: SourceOfferId,
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

        public static AcceptedOrderSource AcceptedSource(TestClock clock)
        {
            var departure = clock.GetDateTime().AddDays(30);

            var segment = new AcceptedSegment(
                SegmentRef: SegmentRef,
                Sequence: 1,
                FlightSourceId: FlightId,
                FlightVersion: 1,
                FlightNumber: "W5 1234",
                OriginAirportId: 100,
                OriginAirportTerminalId: null,
                DestinationAirportId: 200,
                DestinationAirportTerminalId: null,
                MarketingAirlineId: 10,
                OperatingAirlineId: 10,
                DepartureAt: departure,
                ArrivalAt: departure.AddHours(2),
                DurationMinutes: 120,
                AircraftId: 1,
                CabinClassId: 1,
                RbdId: 1,
                BookingClass: "Y",
                BookingClassCode: "Y",
                CapacityReference: 77,
                FareReference: AirFareId,
                Legs: Array.Empty<AcceptedSegmentLeg>());

            var product = new AcceptedProduct(
                ProductRef: ProductRef,
                TravellerRef: TravellerRef,
                ProductType: ProductType.AirFare,
                Quantity: 1m,
                UnitOfMeasure: OrderItemUnitOfMeasure.PassengerFare,
                Snapshot: new AcceptedProductSnapshot(
                    ProductType.AirFare,
                    AirFareId.ToString(),
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
                CommercialTerms: new AcceptedCommercialTerms(
                    RefundabilitySummary: CommercialTermState.Permitted,
                    ChangeabilitySummary: CommercialTermState.Permitted,
                    UpgradeEligibilitySummary: CommercialTermState.Prohibited,
                    SourceSystem: SourceSystem,
                    SourcePolicyReference: null),
                Services: new[]
                {
                    new AcceptedService(
                        ServiceRef: ServiceRef,
                        ServiceType: OrderServiceType.AirTransportation,
                        ServiceCode: "AIR",
                        Name: "Air transportation",
                        DeliveryModel: DeliveryModel.PerPassengerSegment,
                        PriceTreatment: ServicePriceTreatment.SeparatelyPriced,
                        RequiresReservation: true,
                        RequiresSupplierConfirmation: false,
                        RequiresDocument: true,
                        ProviderType: OrderProviderType.Airline,
                        BeneficiaryTravellerRefs: [TravellerRef],
                        Detail: new AcceptedAirTransportDetail(SegmentRef, "YOW"),
                        DocumentKind: ServiceDocumentKind.ElectronicTicket)
                });

            return new AcceptedOrderSource(
                SourceSystem: SourceSystem,
                SourceOfferId: SourceOfferId,
                SaleCurrencyId: CurrencyId,
                TicketingDeadline: clock.GetDateTime().AddDays(1),
                Travellers: new[] { new AcceptedSourceTraveller(TravellerRef, 1) },
                Journeys: new[]
                {
                    new AcceptedJourney(JourneyRef, 1, 100, 200, BoundDirection.Outbound, new[] { segment })
                },
                Products: new[] { product },
                PricingLines: new[]
                {
                    AcceptedLine(PricingComponentType.Fare, 1_000_000m, "YOW", null, "900"),
                    AcceptedLine(PricingComponentType.Tax, 90_000m, "I6", "Value added tax", "TAX-1")
                });
        }

        private static AcceptedSourcePricingLine AcceptedLine(
            PricingComponentType componentType,
            decimal amount,
            string? code,
            string? description,
            string sourceCode)
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
                $"{SourceOfferId}:{TravellerRef}:{JourneyRef}:{FlightId}:{sourceCode}",
                "1",
                ProductRef,
                ServiceRef,
                code,
                description,
                null);
    }
}
