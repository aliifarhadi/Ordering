namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedAddedSeatDetail(
        long AssociatedAirOrderServiceId,
        string? SoldSeatNumber = null) : AcceptedServiceDetail;
}
