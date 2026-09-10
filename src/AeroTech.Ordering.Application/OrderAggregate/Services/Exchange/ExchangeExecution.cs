namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeExecution(
        long OrderId,
        long PredecessorOrderServiceId,
        string QuotedExchangeId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion);
}
