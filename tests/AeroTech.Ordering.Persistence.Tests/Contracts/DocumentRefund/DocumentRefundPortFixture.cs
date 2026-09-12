using AeroTech.Ordering.Domain.Ports.DocumentRefund;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.DocumentRefund
{
    internal static class DocumentRefundPortFixture
    {
        public const long OrderId = 9_100_009L;
        public const long OperationId = 9_500_009L;
        public const long OtherOperationId = 9_500_010L;
        public const string DocumentNumber = "M9300001";

        public static readonly IReadOnlyList<int> CouponNumbers = [1, 2];

        public const string Key = "emd-refund:M9300001:1";
        public const string OtherKey = "emd-refund:M9300001:2";
        public const string NeverDispatchedKey = "emd-refund:M9300001:0";

        public static DocumentRefundRequest Request(string operationKey = Key) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            DocumentNumber,
            CouponNumbers);

        public static DocumentRefundRecoveryRequest Recovery(string operationKey) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            DocumentNumber);

        public static DocumentRefundEligibilityRequest Eligibility(string operationKey = Key) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            DocumentNumber,
            CouponNumbers);

        public static IEnumerable<DocumentRefundRequest> ConflictingRequests()
        {
            var request = Request();

            yield return request with { DocumentNumber = "M9399999" };
            yield return request with { CouponNumbers = [1] };
            yield return request with { CouponNumbers = [3, 4] };
            yield return request with { OrderId = request.OrderId + 1 };
            yield return request with { OperationId = request.OperationId + 1 };
        }
    }
}
