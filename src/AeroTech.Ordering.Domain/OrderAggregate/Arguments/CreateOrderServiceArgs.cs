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
        ServicePriceTreatment PriceTreatment,
        bool RequiresReservation,
        bool RequiresSupplierConfirmation,
        bool RequiresDocument,
        OrderProviderType ProviderType,
        DateTimeOffset CreatedAt,
        ServiceDocumentKind? DocumentKind = null,
        bool RequiresPaymentCoverage = false,
        string? SupplierCode = null,
        string? DeliveryProviderReference = null);
}
