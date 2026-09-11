using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.Ports.AncillaryDisposition;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.AncillaryDisposition
{
    internal static class AncillaryDispositionPortFixture
    {
        public const long OrderId = 9_100_010L;
        public const long OperationId = 9_500_012L;
        public const string QuotedExchangeId = "EXC-QUOTE-ANC-CONTRACT-1";
        public const string OtherQuotedExchangeId = "EXC-QUOTE-ANC-CONTRACT-2";
        public const string PredecessorDocumentNumber = "T9200001";
        public const string OtherPredecessorDocumentNumber = "T9200002";
        public const string EmdDocumentNumber = "M900001";
        public const string SecondEmdDocumentNumber = "M900002";
        public const string ReasonForIssuanceSubCode = "0DF";

        public const int FlownCouponNumber = 1;
        public const int FirstReissuedCouponNumber = 2;
        public const int SecondReissuedCouponNumber = 3;

        public static IReadOnlyList<int> ReissueScope => [FirstReissuedCouponNumber, SecondReissuedCouponNumber];

        public static AncillaryExchangeDispositionRequest Request(
            string quotedExchangeId = QuotedExchangeId,
            string predecessorDocumentNumber = PredecessorDocumentNumber)
            => Bound(new AncillaryExchangeDispositionRequest(
                OrderId,
                OperationId,
                quotedExchangeId,
                predecessorDocumentNumber,
                ReissueScope,
                [
                    Affected(EmdDocumentNumber, FirstReissuedCouponNumber, predecessorDocumentNumber),
                    Affected(SecondEmdDocumentNumber, SecondReissuedCouponNumber, predecessorDocumentNumber)
                ],
                string.Empty));

        public static AncillaryExchangeDispositionRequest SingleCouponRequest()
            => Bound(new AncillaryExchangeDispositionRequest(
                OrderId,
                OperationId,
                QuotedExchangeId,
                PredecessorDocumentNumber,
                ReissueScope,
                [Affected(EmdDocumentNumber, FirstReissuedCouponNumber, PredecessorDocumentNumber)],
                string.Empty));

        public static string Key(string emdDocumentNumber, int emdCouponNumber)
            => $"{emdDocumentNumber}:{emdCouponNumber}";

        private static AncillaryExchangeDispositionRequest Bound(AncillaryExchangeDispositionRequest request)
            => request with { ContextFingerprint = ExchangeAncillaryPlanner.Fingerprint(request) };

        private static AffectedAncillaryCoupon Affected(
            string emdDocumentNumber,
            int predecessorCouponNumber,
            string predecessorDocumentNumber)
            => new(
                emdDocumentNumber,
                1,
                ElectronicMiscDocumentType.Associated,
                EmdCouponPurpose.Fee,
                ReasonForIssuanceSubCode,
                predecessorDocumentNumber,
                predecessorCouponNumber);
    }
}
