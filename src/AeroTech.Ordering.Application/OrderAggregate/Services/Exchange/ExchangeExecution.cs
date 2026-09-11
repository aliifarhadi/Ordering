namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeExecution(
        long OrderId,
        IReadOnlyList<long> ChangedOrderServiceIds,
        string QuotedExchangeId,
        string IdempotencyKey,
        int? ExpectedCommercialVersion,
        string? FundingMethodRef = null);
}
