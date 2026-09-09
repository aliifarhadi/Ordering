namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record CancelOrderRequest(int? ExpectedCommercialVersion);
}
