using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderServiceArgs(
        long Id,
        long OrderId,
        long OrderItemId,
        OrderServiceType ServiceType,
        string ServiceCode,
        string Name,
        DeliveryModel DeliveryModel,
        bool RequiresFulfillment,
        bool RequiresSupplierConfirmation,
        bool RequiresDocument,
        OrderProviderType ProviderType,
        string? SupplierCode,
        DateTimeOffset CreatedAt);
}
