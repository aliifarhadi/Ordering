namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record AcceptedServicingFeeDocument(
        string DocumentReference,
        string SourceReference,
        long IssuerCarrierId,
        long? TravelerId,
        string ReasonForIssuanceCode,
        int CurrencyId,
        IReadOnlyList<AcceptedServicingFeeDocumentCoupon> Coupons);
}
