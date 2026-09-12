using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Domain.Ports.AncillaryDisposition
{
    public sealed record AncillaryEmdExchangeTerms(
        string ExchangeGroupRef,
        ElectronicMiscDocumentType SuccessorType,
        string SuccessorReasonForIssuanceCode,
        int CurrencyId,
        IReadOnlyList<AncillaryEmdExchangeSuccessorCoupon> SuccessorCoupons,
        string SourceReference,
        PricingSource PricingSource,
        IReadOnlyList<AcceptedRefundPricingLine> PricingLines,
        AcceptedAddCollect? AddCollect = null,
        AcceptedRefundDue? RefundDue = null,
        AcceptedResidual? Residual = null,
        string? FundingMethodRef = null);
}
