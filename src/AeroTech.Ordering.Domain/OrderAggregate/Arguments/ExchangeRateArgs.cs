namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record ExchangeRateArgs(
        decimal RateOfExchange,
        int NumberOfDecimalPlaces,
        string? RateOfExchangeId,
        int RoundingFactor);
}
