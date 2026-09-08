namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedAirServiceDetail(
        long FareReference,
        string? FareBasis,
        string? FareFamily,
        AcceptedBaggageAllowance? CheckedBaggage,
        AcceptedBaggageAllowance? CabinBaggage);
}
