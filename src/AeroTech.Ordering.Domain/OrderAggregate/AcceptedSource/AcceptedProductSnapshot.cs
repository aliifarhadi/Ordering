using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedProductSnapshot(
        ProductType ProductType,
        string SourceProductReference,
        string? ProductCode,
        string? ProductName,
        string? Brand,
        int? MarketingAirlineId,
        int? OperatingAirlineId,
        string? SupplierCode,
        string SourceSystem,
        string SourceOfferId,
        string? SourcePricingReference);
}
