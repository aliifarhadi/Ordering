namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedAirServiceDetail(
        long FareReference,
        string? FareBasis,
        string? FareFamily,
        long? FareNumber,
        bool IsChangeable,
        bool IsRefundable,
        bool IsUpgradable,
        AcceptedBaggageAllowance? CheckedBaggage,
        AcceptedBaggageAllowance? CabinBaggage);
}
