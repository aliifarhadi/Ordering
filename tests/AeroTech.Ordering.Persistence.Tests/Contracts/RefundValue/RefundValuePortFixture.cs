using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.RefundValue;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.RefundValue
{
    internal static class RefundValuePortFixture
    {
        public const long OrderId = 9_100_006L;
        public const long OperationId = 9_500_006L;
        public const long OtherOperationId = 9_500_007L;
        public const int CurrencyId = 1;
        public const decimal Amount = 180_000m;
        public const string PredecessorDocumentNumber = "T9200001";
        public const string SuccessorDocumentNumber = "T9200099";
        public const string SourcePricingReference = "AIRPRICE-EXCHANGE-PRICING-CONTRACT-1";

        public const string Key = "exchange-refund-value:9200001:9500006";
        public const string OtherKey = "exchange-refund-value:9200001:9500007";
        public const string NeverDispatchedKey = "exchange-refund-value:9200001:0";

        public static RefundValueRequest Request(string operationKey = Key) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            PredecessorDocumentNumber,
            Amount,
            CurrencyId,
            AcceptedRefundDue.OriginalFormOfPayment,
            null,
            SuccessorDocumentNumber,
            SourcePricingReference);

        public static RefundValueRecoveryRequest Recovery(string operationKey) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId);

        public static IEnumerable<RefundValueRequest> ConflictingRequests()
        {
            var request = Request();

            yield return request with { ApprovedAmount = request.ApprovedAmount + 1m };
            yield return request with { CurrencyId = request.CurrencyId + 7 };
            yield return request with { ApprovedDisposition = "SomewhereElse" };
            yield return request with { DocumentNumber = "T9999999" };
            yield return request with { SuccessorDocumentNumber = "T9999998" };
            yield return request with { OrderId = request.OrderId + 1 };
            yield return request with { OperationId = request.OperationId + 1 };
        }
    }
}
