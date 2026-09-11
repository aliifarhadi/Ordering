using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeFunding
{
    internal static class ExchangeFundingPortFixture
    {
        public const long OrderId = 9_100_004L;
        public const long OperationId = 9_500_004L;
        public const long OtherOperationId = 9_500_005L;
        public const long PayerTravellerId = 9_600_001L;
        public const int CurrencyId = 1;
        public const decimal Amount = 250_000m;
        public const string QuotedExchangeId = "EXC-QUOTE-CONTRACT-1";
        public const string PredecessorDocumentNumber = "T9200001";
        public const string SuccessorDocumentNumber = "T9200099";
        public const string FundingMethodRef = "FOP-CONTRACT-1";

        public const string GuaranteeKey = "exchange-funding-guarantee:9200001:9500004";
        public const string CaptureKey = "exchange-funding-capture:9200001:9500004";
        public const string ReleaseKey = "exchange-funding-release:9200001:9500004";
        public const string OtherGuaranteeKey = "exchange-funding-guarantee:9200001:9500005";
        public const string NeverDispatchedKey = "exchange-funding-guarantee:9200001:0";

        public static ExchangeFundingGuaranteeRequest Guarantee(string operationKey = GuaranteeKey) => new(
            operationKey,
            OrderId,
            operationKey == OtherGuaranteeKey ? OtherOperationId : OperationId,
            QuotedExchangeId,
            PredecessorDocumentNumber,
            PayerTravellerId,
            Amount,
            CurrencyId,
            FundingMethodRef);

        public static ExchangeFundingCaptureRequest Capture(string? guaranteeReference) => new(
            CaptureKey,
            OrderId,
            OperationId,
            QuotedExchangeId,
            SuccessorDocumentNumber,
            guaranteeReference,
            Amount,
            CurrencyId);

        public static ExchangeFundingReleaseRequest Release(string? guaranteeReference) => new(
            ReleaseKey,
            OrderId,
            OperationId,
            QuotedExchangeId,
            guaranteeReference,
            ExchangeFundingReleaseReason.DocumentRejected);

        public static ExchangeFundingRecoveryRequest Recovery(string operationKey) => new(
            operationKey,
            OrderId,
            operationKey == OtherGuaranteeKey ? OtherOperationId : OperationId);
    }
}
