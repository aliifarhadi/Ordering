namespace AeroTech.Ordering.Domain.Ports.EmdAssociation
{
    public sealed record EmdReassociationRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string EmdDocumentNumber,
        int EmdCouponNumber,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber,
        string SuccessorDocumentNumber,
        int SuccessorCouponNumber,
        long? BeneficiaryTravellerId,
        long IssuerCarrierId,
        string DecisionReference);
}
