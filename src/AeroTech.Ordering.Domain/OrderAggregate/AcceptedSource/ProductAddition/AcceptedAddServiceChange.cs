using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedAddServiceChange(
        string SourceSystem,
        string QuotedOfferId,
        string SelectedOfferItemId,
        PricingSource PricingSource,
        AcceptedAddedProduct Product,
        IReadOnlyList<AcceptedAdditionPricingLine> PricingLines,
        string? SourcePricingReference = null);
}
