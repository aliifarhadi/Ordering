using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedService(
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
        IReadOnlyList<string> BeneficiaryTravellerRefs,
        AcceptedServiceDetail Detail,
        ServiceDocumentKind? DocumentKind = null,
        bool RequiresPaymentCoverage = false,
        string? SupplierCode = null,
        string? DeliveryProviderReference = null,
        IReadOnlyList<string>? CoveredAirServiceRefs = null,
        IReadOnlyList<string>? CoveredSegmentRefs = null);
}
