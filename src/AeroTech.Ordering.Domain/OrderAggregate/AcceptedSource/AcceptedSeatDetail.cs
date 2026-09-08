namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedSeatDetail(
        string AirServiceRef,
        string? SoldSeatNumber = null) : AcceptedServiceDetail;
}
