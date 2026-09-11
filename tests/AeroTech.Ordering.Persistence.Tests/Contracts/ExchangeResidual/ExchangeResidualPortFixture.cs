using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeResidual;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.ExchangeResidual
{
    internal static class ExchangeResidualPortFixture
    {
        public const long OrderId = 9_100_008L;
        public const long OperationId = 9_500_008L;
        public const long OtherOperationId = 9_500_009L;
        public const long BeneficiaryTravellerId = 9_600_001L;
        public const int CurrencyId = 1;
        public const decimal Amount = 180_000m;
        public const string QuotedExchangeId = "EXC-QUOTE-CONTRACT-1";
        public const string PredecessorDocumentNumber = "T9200001";
        public const string SuccessorDocumentNumber = "T9200099";
        public const string Disposition = "ResidualCredit";
        public const string SourcePricingReference = "AIRPRICE-EXCHANGE-PRICING-CONTRACT-1";

        public const string Key = "exchange-residual:9200001:9500008";
        public const string OtherKey = "exchange-residual:9200001:9500009";
        public const string NeverDispatchedKey = "exchange-residual:9200001:0";

        public static ExchangeResidualRequest Request(string operationKey = Key) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            QuotedExchangeId,
            PredecessorDocumentNumber,
            SuccessorDocumentNumber,
            BeneficiaryTravellerId,
            Amount,
            CurrencyId,
            Disposition,
            ResidualInstrumentKind.Mco,
            SourcePricingReference);

        public static ExchangeResidualRecoveryRequest Recovery(string operationKey) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId);

        public static IEnumerable<ExchangeResidualRequest> ConflictingRequests()
        {
            var request = Request();

            yield return request with { Amount = request.Amount + 1m };
            yield return request with { CurrencyId = request.CurrencyId + 7 };
            yield return request with { Disposition = "SomethingElse" };
            yield return request with { PredecessorDocumentNumber = "T9999999" };
            yield return request with { SuccessorDocumentNumber = "T9999998" };
            yield return request with { BeneficiaryTravellerId = request.BeneficiaryTravellerId + 1 };
            yield return request with { QuotedExchangeId = $"{request.QuotedExchangeId}-OTHER" };
            yield return request with { OrderId = request.OrderId + 1 };
            yield return request with { OperationId = request.OperationId + 1 };
        }
    }
}
