namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments
{
    public sealed record EmdCouponRefund(
        int EmdCouponNumber,
        decimal ApprovedAmount,
        int CurrencyId,
        string ApprovedDisposition,
        long OperationId,
        string DecisionReference,
        string? ProviderReference);
}
