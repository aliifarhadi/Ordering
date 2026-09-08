using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;

namespace AeroTech.Ordering.Domain.Tests._Shared
{
    public static class FareConstructionFactory
    {
        public const string SourceSystem = "PricingEngine";

        public static AcceptedFareConstruction TrueRoundTrip(string travellerRef = "T1") => new(
            ConstructionRef: $"FC-RT-{travellerRef}",
            SourceSystem: SourceSystem,
            ProductRefs: ProductRefsFor(travellerRef),
            PricingGroups:
            [
                new AcceptedFarePricingGroup(
                    PricingGroupRef: $"PG-{travellerRef}",
                    TravellerRefs: [travellerRef],
                    PricingUnits:
                    [
                        new AcceptedFarePricingUnit(
                            PricingUnitRef: $"PU-RT-{travellerRef}",
                            Sequence: 1,
                            FareComponents:
                            [
                                Component($"FC-OUT-{travellerRef}", 1, travellerRef, "B1", MultiPassengerOrderFactory.OutboundFlightId, 100, 200),
                                Component($"FC-IN-{travellerRef}", 2, travellerRef, "B2", MultiPassengerOrderFactory.InboundFlightId, 200, 100)
                            ],
                            PricingUnitType: FarePricingUnitType.RoundTrip,
                            CombinationMethod: FareCombinationMethod.FiledFare)
                    ],
                    PassengerTypeCode: PassengerTypeCode.ADT)
            ],
            ConstructionType: AirFareConstructionType.RoundTrip);

        public static AcceptedFareConstruction TwoOneWays(string travellerRef = "T1") => new(
            ConstructionRef: $"FC-OW2-{travellerRef}",
            SourceSystem: SourceSystem,
            ProductRefs: ProductRefsFor(travellerRef),
            PricingGroups:
            [
                new AcceptedFarePricingGroup(
                    PricingGroupRef: $"PG-{travellerRef}",
                    TravellerRefs: [travellerRef],
                    PricingUnits:
                    [
                        new AcceptedFarePricingUnit(
                            PricingUnitRef: $"PU-OW1-{travellerRef}",
                            Sequence: 1,
                            FareComponents:
                            [
                                Component($"FC-OUT-{travellerRef}", 1, travellerRef, "B1", MultiPassengerOrderFactory.OutboundFlightId, 100, 200)
                            ],
                            PricingUnitType: FarePricingUnitType.OneWay,
                            CombinationMethod: FareCombinationMethod.FiledFare),
                        new AcceptedFarePricingUnit(
                            PricingUnitRef: $"PU-OW2-{travellerRef}",
                            Sequence: 2,
                            FareComponents:
                            [
                                Component($"FC-IN-{travellerRef}", 1, travellerRef, "B2", MultiPassengerOrderFactory.InboundFlightId, 200, 100)
                            ],
                            PricingUnitType: FarePricingUnitType.OneWay,
                            CombinationMethod: FareCombinationMethod.FiledFare)
                    ],
                    PassengerTypeCode: PassengerTypeCode.ADT)
            ],
            ConstructionType: AirFareConstructionType.RoundTripFromOneWays);

        public static AcceptedFareConstruction ThroughFare(string travellerRef = "T1") => new(
            ConstructionRef: $"FC-THRU-{travellerRef}",
            SourceSystem: SourceSystem,
            ProductRefs: ProductRefsFor(travellerRef),
            PricingGroups:
            [
                new AcceptedFarePricingGroup(
                    PricingGroupRef: $"PG-{travellerRef}",
                    TravellerRefs: [travellerRef],
                    PricingUnits:
                    [
                        new AcceptedFarePricingUnit(
                            PricingUnitRef: $"PU-THRU-{travellerRef}",
                            Sequence: 1,
                            FareComponents:
                            [
                                new AcceptedFareComponent(
                                    FareComponentRef: $"FC-THRU-{travellerRef}",
                                    Sequence: 1,
                                    ServiceRefs:
                                    [
                                        ServiceRef(travellerRef, "B1", MultiPassengerOrderFactory.OutboundFlightId),
                                        ServiceRef(travellerRef, "B2", MultiPassengerOrderFactory.InboundFlightId)
                                    ],
                                    SegmentRefs:
                                    [
                                        SegmentRef("B1", MultiPassengerOrderFactory.OutboundFlightId),
                                        SegmentRef("B2", MultiPassengerOrderFactory.InboundFlightId)
                                    ],
                                    OriginAirportId: 100,
                                    DestinationAirportId: 100,
                                    FareBasis: "YTHRU",
                                    BrandCode: "ECOFLEX",
                                    BrandName: "Economy Flex",
                                    TariffReference: "TAR-1",
                                    RuleReference: "RULE-1",
                                    RoutingReference: "ROUTE-1",
                                    SourceFareReference: "SRC-FARE-1")
                            ],
                            PricingUnitType: FarePricingUnitType.RoundTrip,
                            CombinationMethod: FareCombinationMethod.FiledFare)
                    ],
                    PassengerTypeCode: PassengerTypeCode.ADT)
            ],
            ConstructionType: AirFareConstructionType.RoundTrip);

        public static AcceptedFareConstruction Unspecified(string travellerRef = "T1") => new(
            ConstructionRef: $"FC-UNSPEC-{travellerRef}",
            SourceSystem: SourceSystem,
            ProductRefs: ProductRefsFor(travellerRef),
            PricingGroups:
            [
                new AcceptedFarePricingGroup(
                    PricingGroupRef: $"PG-{travellerRef}",
                    TravellerRefs: [travellerRef],
                    PricingUnits:
                    [
                        new AcceptedFarePricingUnit(
                            PricingUnitRef: $"PU-{travellerRef}",
                            Sequence: 1,
                            FareComponents:
                            [
                                Component($"FC-{travellerRef}", 1, travellerRef, "B1", MultiPassengerOrderFactory.OutboundFlightId, 100, 200)
                            ])
                    ])
            ]);

        public static AcceptedFareConstruction SharedGroup() => new(
            ConstructionRef: "FC-GROUP",
            SourceSystem: SourceSystem,
            ProductRefs: [.. ProductRefsFor("T1"), .. ProductRefsFor("T2")],
            PricingGroups:
            [
                new AcceptedFarePricingGroup(
                    PricingGroupRef: "PG-SHARED",
                    TravellerRefs: ["T1", "T2"],
                    PricingUnits:
                    [
                        new AcceptedFarePricingUnit(
                            PricingUnitRef: "PU-SHARED",
                            Sequence: 1,
                            FareComponents:
                            [
                                new AcceptedFareComponent(
                                    FareComponentRef: "FC-SHARED",
                                    Sequence: 1,
                                    ServiceRefs:
                                    [
                                        ServiceRef("T1", "B1", MultiPassengerOrderFactory.OutboundFlightId),
                                        ServiceRef("T2", "B1", MultiPassengerOrderFactory.OutboundFlightId)
                                    ],
                                    SegmentRefs: [SegmentRef("B1", MultiPassengerOrderFactory.OutboundFlightId)],
                                    FareBasis: "YGRP")
                            ],
                            PricingUnitType: FarePricingUnitType.OneWay)
                    ],
                    PassengerTypeCode: PassengerTypeCode.ADT,
                    SourceReference: "GRP-1")
            ],
            ConstructionType: AirFareConstructionType.OneWay);

        public static AcceptedFareComponent Component(
            string componentRef,
            int sequence,
            string travellerRef,
            string journeyRef,
            long flightId,
            int origin,
            int destination) => new(
            FareComponentRef: componentRef,
            Sequence: sequence,
            ServiceRefs: [ServiceRef(travellerRef, journeyRef, flightId)],
            SegmentRefs: [SegmentRef(journeyRef, flightId)],
            OriginAirportId: origin,
            DestinationAirportId: destination,
            FareBasis: "YRTFC");

        public static IReadOnlyList<string> ProductRefsFor(string travellerRef) => [
            $"{travellerRef}:{MultiPassengerOrderFactory.OutboundAirFareId}:{MultiPassengerOrderFactory.FareBasis}",
            $"{travellerRef}:{MultiPassengerOrderFactory.InboundAirFareId}:{MultiPassengerOrderFactory.FareBasis}"
        ];

        public static string ServiceRef(string travellerRef, string journeyRef, long flightId)
            => $"{travellerRef}:{journeyRef}:{flightId}";

        public static string SegmentRef(string journeyRef, long flightId) => $"{journeyRef}:{flightId}";

        public static AcceptedFareConstruction SingleService(
            string constructionRef,
            string travellerRef,
            string journeyRef,
            long flightId,
            string fareBasis,
            string? supersedesConstructionRef = null) => new(
            ConstructionRef: constructionRef,
            SourceSystem: SourceSystem,
            ProductRefs: [],
            PricingGroups:
            [
                new AcceptedFarePricingGroup(
                    PricingGroupRef: $"PG-{constructionRef}",
                    TravellerRefs: [travellerRef],
                    PricingUnits:
                    [
                        new AcceptedFarePricingUnit(
                            PricingUnitRef: $"PU-{constructionRef}",
                            Sequence: 1,
                            FareComponents:
                            [
                                new AcceptedFareComponent(
                                    FareComponentRef: $"FC-{constructionRef}",
                                    Sequence: 1,
                                    ServiceRefs: [ServiceRef(travellerRef, journeyRef, flightId)],
                                    SegmentRefs: [SegmentRef(journeyRef, flightId)],
                                    FareBasis: fareBasis)
                            ],
                            PricingUnitType: FarePricingUnitType.OneWay)
                    ],
                    PassengerTypeCode: PassengerTypeCode.ADT)
            ],
            ConstructionType: AirFareConstructionType.OneWay,
            SupersedesConstructionRef: supersedesConstructionRef);
    }
}
