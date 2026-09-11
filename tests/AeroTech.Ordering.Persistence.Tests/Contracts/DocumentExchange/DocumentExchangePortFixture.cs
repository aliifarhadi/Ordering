using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.DocumentExchange
{
    internal static class DocumentExchangePortFixture
    {
        public const long OrderId = 9_100_003L;
        public const long OperationId = 9_500_003L;
        public const string OperationKey = "document-exchange:9200001:9500003";
        public const string PredecessorDocumentNumber = "T9200001";
        public const string QuotedExchangeId = "EXC-QUOTE-CONTRACT-1";
        public const string TargetSelectionRef = "AIRPRICE-EXCHANGE-TARGET-CONTRACT-1";
        public const string SourcePricingReference = "AIRPRICE-EXCHANGE-PRICING-CONTRACT-1";

        public const int UsedCouponNumber = 1;
        public const int ContinuedCouponNumber = 2;
        public const int ReplacedCouponNumber = 3;

        private const int MarketingAirlineId = 10;
        private const string BookingClass = "Y";

        private static readonly DateTimeOffset FirstDeparture = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

        public static DocumentExchangeEligibilityRequest EligibilityRequest() => new(
            OperationKey,
            OrderId,
            OperationId,
            PredecessorDocumentNumber,
            [ContinuedCouponNumber, ReplacedCouponNumber],
            TargetSelectionRef,
            SourcePricingReference);

        public static DocumentExchangeRequest Request() => new(
            OperationKey,
            OrderId,
            OperationId,
            PredecessorDocumentNumber,
            QuotedExchangeId,
            TargetSelectionRef,
            SourcePricingReference,
            [
                new DocumentExchangeCouponRequest(
                    ContinuedCouponNumber,
                    ExchangeCouponDisposition.Continued,
                    Segment(ContinuedCouponNumber, "W5 1002")),
                new DocumentExchangeCouponRequest(
                    ReplacedCouponNumber,
                    ExchangeCouponDisposition.Replaced,
                    Segment(ReplacedCouponNumber, "W5 7777"))
            ]);

        public static DocumentExchangeRecoveryRequest Recovery()
            => new(OperationKey, OrderId, OperationId, PredecessorDocumentNumber);

        public static DocumentExchangeRecoveryRequest UnknownRecovery()
            => new("document-exchange:9200001:0", OrderId, 0L, PredecessorDocumentNumber);

        private static TicketedSegmentSnapshot Segment(int couponNumber, string flightNumber) => new(
            MarketingAirlineId,
            flightNumber,
            100,
            200,
            FirstDeparture.AddDays(couponNumber),
            FirstDeparture.AddDays(couponNumber).AddHours(2),
            BookingClass);
    }
}
