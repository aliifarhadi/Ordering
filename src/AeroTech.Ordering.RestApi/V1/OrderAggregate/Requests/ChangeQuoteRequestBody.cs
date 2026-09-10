namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record ChangeQuoteRequestBody(
        long? OrderServiceId = null,
        QuoteExchange? QuoteExchange = null);
}
