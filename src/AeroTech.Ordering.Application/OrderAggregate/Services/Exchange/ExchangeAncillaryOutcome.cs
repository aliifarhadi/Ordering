using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed record ExchangeAncillaryOutcome(
        string LegIdentity,
        long ElectronicMiscDocumentId,
        string EmdDocumentNumber,
        int EmdCouponNumber,
        int PredecessorCouponNumber,
        int? TargetPredecessorCouponNumber,
        AncillaryExchangeDisposition Disposition,
        ExchangeAncillaryState State,
        string DecisionReference,
        int DecisionVersion,
        string? ProviderReference,
        string? Detail);
}
