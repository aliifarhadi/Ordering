using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedProductAddition(
        string SourceSystem,
        string SourceReference,
        PricingSource PricingSource,
        AcceptedAddedProduct Product,
        IReadOnlyList<AcceptedAdditionPricingLine> PricingLines,
        string? SourceOfferId = null,
        string? SourcePricingReference = null);
}
