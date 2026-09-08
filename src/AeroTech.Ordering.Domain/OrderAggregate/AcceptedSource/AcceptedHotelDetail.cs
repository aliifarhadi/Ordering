namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedHotelDetail(
        string PropertyReference,
        DateOnly CheckIn,
        DateOnly CheckOut,
        int RoomCount,
        int GuestCount,
        string? SupplierReference = null,
        string? RoomTypeCode = null,
        string? RatePlanReference = null) : AcceptedServiceDetail;
}
