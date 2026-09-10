namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record CancelRefundRequest(
        string Reason,
        int? ExpectedCommercialVersion,
        string? ReasonDetail = null);
}
