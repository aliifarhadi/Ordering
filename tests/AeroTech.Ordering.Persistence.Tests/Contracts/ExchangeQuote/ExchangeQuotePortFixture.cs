using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain.Ports.Exchange;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeQuote
{
    internal static class ExchangeQuotePortFixture
    {
        public const long OrderId = 9_100_001L;
        public const int CommercialVersion = 3;
        public const int SaleCurrencyId = 1;
        public const long OperationId = 9_500_001L;
        public const string OperationKey = "exchange-quote:9500001";
        public const long PredecessorElectronicTicketId = 9_200_001L;
        public const string PredecessorDocumentNumber = "T9200001";

        public const int UsedCouponNumber = 1;
        public const int ContinuedCouponNumber = 2;
        public const int ChangedCouponNumber = 3;

        public const long UsedTicketCouponId = 9_300_001L;
        public const long ContinuedTicketCouponId = 9_300_002L;
        public const long ChangedTicketCouponId = 9_300_003L;

        public const long UsedOrderServiceId = 9_400_001L;
        public const long ContinuedOrderServiceId = 9_400_002L;
        public const long ChangedOrderServiceId = 9_400_003L;

        public const string ReplacementServiceRef = "CONTRACT-REPLACEMENT-1";
        public const string ReplacementSegmentRef = "CONTRACT-REPLACEMENT-SEG-1";
        public const string ReplacementFlightNumber = "W5 7777";
        public const long TravellerId = 9_600_001L;

        private const int MarketingAirlineId = 10;
        private const string BookingClass = "Y";

        private static readonly DateTimeOffset FirstDeparture = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        public static ExchangeQuoteRequest Request() => new(
            OrderId,
            CommercialVersion,
            PredecessorElectronicTicketId,
            PredecessorDocumentNumber,
            [ChangedOrderServiceId],
            [
                new ExchangeScopeCoupon(
                    ContinuedTicketCouponId,
                    ContinuedCouponNumber,
                    ContinuedOrderServiceId,
                    false,
                    Segment(ContinuedCouponNumber),
                    Segment(ContinuedCouponNumber)),
                new ExchangeScopeCoupon(
                    ChangedTicketCouponId,
                    ChangedCouponNumber,
                    ChangedOrderServiceId,
                    true,
                    Segment(ChangedCouponNumber),
                    Segment(ChangedCouponNumber))
            ],
            [
                new HistoricalUsedCoupon(
                    UsedTicketCouponId,
                    UsedCouponNumber,
                    TicketCouponFinancialStatus.Used,
                    UsedOrderServiceId,
                    Segment(UsedCouponNumber),
                    null)
            ],
            [
                Fare(UsedCouponNumber, 1_000_000m),
                Tax(UsedCouponNumber, 90_000m),
                Fare(ContinuedCouponNumber, 1_100_000m),
                Tax(ContinuedCouponNumber, 99_000m),
                Fare(ChangedCouponNumber, 1_200_000m),
                Tax(ChangedCouponNumber, 108_000m)
            ],
            [],
            SaleCurrencyId);

        public static ExchangeQuoteRequest RequestWithStoredFareConstruction()
            => Request() with { FareConstructions = [FareConstruction()] };

        public static AcceptedQuotedExchangeSelection Selection(string quotedExchangeId) => new(
            OperationKey,
            OrderId,
            OperationId,
            quotedExchangeId,
            CommercialVersion,
            PredecessorElectronicTicketId,
            [ChangedOrderServiceId],
            SaleCurrencyId);

        public static FareConstructionContext FareConstruction() => new(
            "AirPrice",
            "AIRPRICE-FARE-CONSTRUCTION-CONTRACT-1",
            AirFareConstructionType.RoundTrip,
            [
                new FareConstructionGroupContext(
                    PassengerTypeCode.ADT,
                    "GROUP-1",
                    [TravellerId],
                    [
                        new FareConstructionUnitContext(
                            1,
                            FarePricingUnitType.RoundTrip,
                            FareCombinationMethod.FiledFare,
                            "UNIT-1",
                            [
                                Component(ContinuedCouponNumber, ContinuedOrderServiceId),
                                Component(ChangedCouponNumber, ChangedOrderServiceId)
                            ])
                    ])
            ]);

        private static FareConstructionComponentContext Component(int sequence, long orderServiceId) => new(
            sequence,
            100,
            200,
            "YRT",
            null,
            null,
            1,
            1,
            BookingClass,
            MarketingAirlineId,
            "TARIFF-1",
            "RULE-1",
            "ROUTING-1",
            $"FARE-{sequence}",
            $"COMPONENT-{sequence}",
            [orderServiceId],
            [9_450_000L + sequence]);

        public static IReadOnlyDictionary<long, AcceptedChangeReplacement> Replacements()
            => new Dictionary<long, AcceptedChangeReplacement> { [ChangedOrderServiceId] = Replacement() };

        public static string CorrelationRefOf(int couponNumber, PricingComponentType componentType)
            => $"XPL-CONTRACT-{couponNumber}-{componentType}";

        private static AcceptedChangeReplacement Replacement() => new(
            ReplacementServiceRef,
            "AIR",
            "Air transportation",
            new AcceptedSegment(
                ReplacementSegmentRef,
                ChangedCouponNumber,
                5_000L + ChangedCouponNumber,
                1,
                ReplacementFlightNumber,
                100,
                null,
                200,
                null,
                MarketingAirlineId,
                MarketingAirlineId,
                FirstDeparture.AddDays(ChangedCouponNumber + 2),
                FirstDeparture.AddDays(ChangedCouponNumber + 2).AddHours(2),
                120,
                1,
                1,
                1,
                "Q",
                BookingClass,
                987_654L,
                900L,
                []),
            new AcceptedAirTransportDetail(ReplacementSegmentRef),
            [TravellerId]);

        private static TicketedSegmentSnapshot Segment(int couponNumber) => new(
            MarketingAirlineId,
            $"W5 100{couponNumber}",
            100,
            200,
            FirstDeparture.AddDays(couponNumber),
            FirstDeparture.AddDays(couponNumber).AddHours(2),
            BookingClass);

        private static PredecessorPricingEvidence Fare(int couponNumber, decimal amount)
            => Evidence(couponNumber, PricingComponentType.Fare, "YRT", amount);

        private static PredecessorPricingEvidence Tax(int couponNumber, decimal amount)
            => Evidence(couponNumber, PricingComponentType.Tax, "I6", amount);

        private static PredecessorPricingEvidence Evidence(
            int couponNumber,
            PricingComponentType componentType,
            string code,
            decimal amount)
            => new(
                CorrelationRefOf(couponNumber, componentType),
                $"OFFER:{couponNumber}:{componentType}",
                null,
                couponNumber,
                componentType,
                code,
                amount,
                SaleCurrencyId,
                amount);
    }
}
