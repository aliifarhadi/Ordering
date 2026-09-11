using AeroTech.Ordering.Domain.Ports.EmdAssociation;

namespace AeroTech.Ordering.Persistence.Tests.Contracts.EmdAssociation
{
    internal static class EmdAssociationPortFixture
    {
        public const long OrderId = 9_100_009L;
        public const long OperationId = 9_500_010L;
        public const long OtherOperationId = 9_500_011L;
        public const long BeneficiaryTravellerId = 9_600_002L;
        public const long IssuerCarrierId = 77L;
        public const string EmdDocumentNumber = "E9300001";
        public const int EmdCouponNumber = 1;
        public const string PredecessorDocumentNumber = "T9200001";
        public const int PredecessorCouponNumber = 2;
        public const string SuccessorDocumentNumber = "T9200099";
        public const int SuccessorCouponNumber = 2;
        public const string DecisionReference = "ANC-DECISION-CONTRACT-1";

        public const string Key = "emd-reassociate:E9300001:1:9500010";
        public const string OtherKey = "emd-reassociate:E9300001:1:9500011";
        public const string NeverDispatchedKey = "emd-reassociate:E9300001:1:0";

        public static EmdReassociationRequest Request(string operationKey = Key) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            EmdDocumentNumber,
            EmdCouponNumber,
            PredecessorDocumentNumber,
            PredecessorCouponNumber,
            SuccessorDocumentNumber,
            SuccessorCouponNumber,
            BeneficiaryTravellerId,
            IssuerCarrierId,
            DecisionReference);

        public static EmdAssociationRecoveryRequest Recovery(string operationKey) => new(
            operationKey,
            OrderId,
            operationKey == OtherKey ? OtherOperationId : OperationId,
            EmdDocumentNumber,
            EmdCouponNumber);

        public static IEnumerable<EmdReassociationRequest> ConflictingRequests()
        {
            var request = Request();

            yield return request with { EmdDocumentNumber = "E9399999" };
            yield return request with { EmdCouponNumber = request.EmdCouponNumber + 1 };
            yield return request with { PredecessorDocumentNumber = "T9999999" };
            yield return request with { PredecessorCouponNumber = request.PredecessorCouponNumber + 1 };
            yield return request with { SuccessorDocumentNumber = "T9999998" };
            yield return request with { SuccessorCouponNumber = request.SuccessorCouponNumber + 1 };
            yield return request with { BeneficiaryTravellerId = request.BeneficiaryTravellerId + 1 };
            yield return request with { IssuerCarrierId = request.IssuerCarrierId + 1 };
            yield return request with { DecisionReference = $"{request.DecisionReference}-OTHER" };
            yield return request with { OrderId = request.OrderId + 1 };
            yield return request with { OperationId = request.OperationId + 1 };
        }
    }
}
