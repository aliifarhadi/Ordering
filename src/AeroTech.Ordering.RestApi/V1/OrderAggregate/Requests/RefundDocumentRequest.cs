namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record RefundDocumentRequest(
        IReadOnlyList<long> TicketCouponIds,
        int? ExpectedCommercialVersion,
        string? QuotedRefundId = null,
        ManualRefundRequest? Manual = null);
}
