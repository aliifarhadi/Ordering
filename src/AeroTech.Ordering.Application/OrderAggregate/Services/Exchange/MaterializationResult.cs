namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    internal sealed record MaterializationResult(MaterializedExchange Materialized, string? Conflict);
}
