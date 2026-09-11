namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments
{
    public sealed record EmdCouponReassociation(
        int EmdCouponNumber,
        long PredecessorTicketCouponId,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber,
        long SuccessorTicketCouponId,
        string SuccessorDocumentNumber,
        int SuccessorCouponNumber,
        long OperationId,
        string DecisionReference,
        string? ProviderReference);
}
