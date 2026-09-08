using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedPricingLineArgs(
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
        long? OrderItemId = null,
        string? Code = null,
        string? Description = null,
        ExchangeRate? ExchangeRate = null,
        PricingApplicationLevel? ApplicationLevel = null,
        decimal? Quantity = null,
        string? UnitOfMeasure = null,
        decimal? UnitPrice = null,
        long? BasisReferenceId = null,
        string? SourceLineRef = null,
        long? OriginalPricingLineId = null,
        long? OriginalAllocationId = null,
        string? TransferGroupId = null,
        long? RelatedOperationId = null,
        string? CalculationSnapshot = null,
        string? TaxDetails = null,
        string? SettlementPartyRef = null,
        string? SettlementCategory = null,
        IReadOnlyList<AcceptedPricingAllocationSetArgs>? AllocationSets = null);
}
