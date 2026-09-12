using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.EmdExchange;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.EmdExchange
{
    internal static class EmdExchangePortFixture
    {
        public const long OrderId = 9_100_011L;
        public const long OperationId = 9_500_011L;
        public const long OtherOperationId = 9_500_012L;
        public const long TravellerId = 9_700_011L;
        public const int CurrencyId = 1;
        public const decimal Value = 60_000m;
        public const string SourceDocumentNumber = "M9400001";
        public const string SuccessorTicketDocumentNumber = "T9400099";
        public const string GroupRef = "EMDX-CONTRACT-1";
        public const string OtherGroupRef = "EMDX-CONTRACT-2";
        public const string DecisionReference = "ANC-DISPOSITION";
        public const string SourcePricingReference = "ANC-EXCHANGE-SOURCE";

        public const string Key = "emd-exchange:EMDX-CONTRACT-1";
        public const string OtherKey = "emd-exchange:EMDX-CONTRACT-2";
        public const string NeverDispatchedKey = "emd-exchange:EMDX-CONTRACT-0";

        public static EmdExchangeRequest Request(string operationKey = Key) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            operationKey == OtherKey ? OtherGroupRef : GroupRef,
            SourceDocumentNumber,
            [1],
            TravellerId,
            ElectronicMiscDocumentType.Associated,
            "A",
            CurrencyId,
            [new EmdExchangeSuccessorCouponRequest(EmdCouponPurpose.Fee, "0DF", Value, 1)],
            SuccessorTicketDocumentNumber,
            DecisionReference,
            SourcePricingReference);

        public static EmdExchangeRequest ResidualRequest() => Request() with
        {
            Residual = new ExchangeCoupledResidualRequest(
                5_000m, CurrencyId, "OriginalFormOfPayment", ResidualInstrumentKind.Emd)
        };

        public static EmdExchangeRecoveryRequest Recovery(string operationKey) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            SourceDocumentNumber);

        public static IEnumerable<EmdExchangeRequest> ConflictingRequests()
        {
            var request = Request();

            yield return request with { SourceDocumentNumber = "M9499999" };
            yield return request with { SourceCouponNumbers = [1, 2] };
            yield return request with { SuccessorType = ElectronicMiscDocumentType.Standalone };
            yield return request with { CurrencyId = request.CurrencyId + 7 };
            yield return request with { SuccessorReasonForIssuanceCode = "Z" };
            yield return request with { SuccessorTicketDocumentNumber = "T9499998" };
            yield return request with { BeneficiaryTravellerId = request.BeneficiaryTravellerId + 1 };
            yield return request with { ExchangeGroupRef = "EMDX-OTHER" };
            yield return request with { OrderId = request.OrderId + 1 };
            yield return request with { OperationId = request.OperationId + 1 };
            yield return request with
            {
                SuccessorCoupons = [new EmdExchangeSuccessorCouponRequest(EmdCouponPurpose.Fee, "0ZZ", Value, 1)]
            };
        }
    }
}
