namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedGroundTransportDetail(
        string PickupLocationReference,
        string DropoffLocationReference,
        DateTimeOffset PickupAt,
        int PassengerCount,
        string? VehicleTypeCode = null) : AcceptedServiceDetail;
}
