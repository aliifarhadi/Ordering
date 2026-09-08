using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition
{
    public sealed record AcceptedAdditionPricingLine(
        PricingComponentType ComponentType,
        PricingEffect Effect,
        OrderPricingLineDirection Direction,
        PricingLineRole LineRole,
        decimal OriginalAmount,
        int OriginalCurrencyId,
        decimal SaleAmount,
        int SaleCurrencyId,
        PricingBasisType BasisType,
        RefundabilityRule Refundability,
        string? ProductRef = null,
        string? ServiceRef = null,
        string? Code = null,
        string? Description = null,
        ExchangeRate? ExchangeRate = null,
        PricingApplicationLevel? ApplicationLevel = null,
        decimal? Quantity = null,
        string? UnitOfMeasure = null,
        decimal? UnitPrice = null,
        string? SourceLineRef = null,
        string? OccurrenceKey = null,
        string? SettlementPartyRef = null,
        string? SettlementCategory = null,
        IReadOnlyList<AcceptedAdditionAllocationSet>? AllocationSets = null);
}
