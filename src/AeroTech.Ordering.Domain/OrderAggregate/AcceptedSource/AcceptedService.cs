using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedService(
        string ServiceRef,
        string TravellerRef,
        string SegmentRef,
        OrderServiceType ServiceType,
        string ServiceCode,
        string Name,
        DeliveryModel DeliveryModel,
        bool RequiresFulfillment,
        bool RequiresSupplierConfirmation,
        bool RequiresDocument,
        OrderProviderType ProviderType,
        string? SupplierCode,
        AcceptedAirServiceDetail? AirTransport);
}
