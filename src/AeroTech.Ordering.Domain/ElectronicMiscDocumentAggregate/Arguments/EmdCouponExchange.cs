namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments
{
    public sealed record EmdCouponExchange(
        int EmdCouponNumber,
        long SuccessorElectronicMiscDocumentId,
        string SuccessorDocumentNumber,
        int SuccessorCouponNumber,
        long OperationId,
        string DecisionReference,
        string? ProviderReference);
}
