using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedAddedService(
        string ServiceRef,
        OrderServiceType ServiceType,
        string ServiceCode,
        string Name,
        DeliveryModel DeliveryModel,
        ServicePriceTreatment PriceTreatment,
        bool RequiresReservation,
        bool RequiresSupplierConfirmation,
        bool RequiresDocument,
        OrderProviderType ProviderType,
        IReadOnlyList<long> BeneficiaryTravellerIds,
        AcceptedServiceDetail Detail,
        ServiceDocumentKind? DocumentKind = null,
        bool RequiresPaymentCoverage = false,
        string? SupplierCode = null,
        string? DeliveryProviderReference = null,
        IReadOnlyList<long>? CoveredOrderServiceIds = null,
        IReadOnlyList<long>? CoveredOrderSegmentIds = null);
}
