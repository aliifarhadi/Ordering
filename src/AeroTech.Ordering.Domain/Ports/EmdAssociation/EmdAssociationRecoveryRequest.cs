namespace AeroTech.Ordering.Domain.Ports.EmdAssociation
{
    public sealed record EmdAssociationRecoveryRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string EmdDocumentNumber,
        int EmdCouponNumber);
}
