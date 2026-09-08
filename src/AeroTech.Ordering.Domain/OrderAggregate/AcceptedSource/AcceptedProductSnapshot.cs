using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource
{
    public sealed record AcceptedProductSnapshot(
        ProductType ProductType,
        string SourceProductReference,
        string SourceSystem,
        string SourceOfferId,
        string? ProductCode = null,
        string? ProductName = null,
        string? BrandCode = null,
        string? BrandName = null,
        int? MarketingAirlineId = null,
        int? OperatingAirlineId = null,
        string? SupplierCode = null,
        string? SourcePricingReference = null);
}
