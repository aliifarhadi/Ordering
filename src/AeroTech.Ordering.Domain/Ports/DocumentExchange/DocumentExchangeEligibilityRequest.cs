namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record DocumentExchangeEligibilityRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string PredecessorDocumentNumber,
        IReadOnlyList<int> PredecessorCouponNumbers,
        string TargetSelectionRef,
        string? SourcePricingReference);
}
