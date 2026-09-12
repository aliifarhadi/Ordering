namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record DocumentExchangeRequest(
        string OperationKey,
        long OrderId,
        long OperationId,
        string PredecessorDocumentNumber,
        string QuotedExchangeId,
        string TargetSelectionRef,
        string? SourcePricingReference,
        IReadOnlyList<DocumentExchangeCouponRequest> Coupons,
        ExchangeCoupledResidualRequest? Residual = null);
}
