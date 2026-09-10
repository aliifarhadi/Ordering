using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange
{
    public sealed record AcceptedExchangePricingLine(
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
        string SourceLineRef,
        long? BasisReferenceId = null,
        long? OrderItemId = null,
        string? PredecessorCorrelationRef = null,
        string? TransferGroupId = null,
        string? Code = null,
        string? Description = null,
        ExchangeRateArgs? ExchangeRate = null,
        PricingApplicationLevel? ApplicationLevel = null,
        string? OccurrenceKey = null,
        string? SettlementPartyRef = null,
        string? SettlementCategory = null);
}
