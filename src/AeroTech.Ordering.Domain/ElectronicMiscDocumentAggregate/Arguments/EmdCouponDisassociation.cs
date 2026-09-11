namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments
{
    public sealed record EmdCouponDisassociation(
        int EmdCouponNumber,
        long PredecessorTicketCouponId,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber,
        long OperationId,
        string DecisionReference,
        string? ProviderReference);
}
