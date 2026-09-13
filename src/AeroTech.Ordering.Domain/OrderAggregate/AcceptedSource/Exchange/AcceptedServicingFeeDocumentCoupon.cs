namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record AcceptedServicingFeeDocumentCoupon(
        string ReasonForIssuanceSubCode,
        string PrimarySourceLineRef,
        decimal DocumentedAmount,
        IReadOnlyList<AcceptedServicingFeeAttribution> Attributions);
}
