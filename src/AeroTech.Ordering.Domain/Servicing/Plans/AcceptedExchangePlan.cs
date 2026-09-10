using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record AcceptedExchangePlan(
        long OperationId,
        long OrderId,
        string QuotedExchangeId,
        string SourceSystem,
        string TargetSelectionRef,
        string? SourcePricingReference,
        PricingSource PricingSource,
        int SaleCurrencyId,
        long PredecessorElectronicTicketId,
        string PredecessorDocumentNumber,
        long PredecessorTravellerId,
        long SuccessorElectronicTicketId,
        int ExpectedCommercialVersion,
        ChangeMonetaryOutcome MonetaryOutcome,
        AcceptedExchange Accepted,
        IReadOnlyList<AcceptedExchangePlanCoupon> Coupons,
        AcceptedExchangeDisposition Disposition = AcceptedExchangeDisposition.Executable,
        string? DispositionDetail = null,
        int? RejectionCode = null,
        int? RejectionHttpStatus = null,
        DocumentExchangeEligibilityOutcome? EligibilityOutcome = null,
        string? EligibilityDetail = null,
        ProviderOperationOutcome ReservationOutcome = ProviderOperationOutcome.Pending,
        string? ReservationExternalRef = null,
        ProviderOperationOutcome? DocumentExchangeOutcome = null,
        string? DocumentExchangeProviderReference = null,
        string? DocumentExchangeDetail = null,
        SuccessorDocumentIdentity? Successor = null)
    {
        public bool IsEligibilityEstablished
            => EligibilityOutcome == DocumentExchangeEligibilityOutcome.Eligible;

        public bool IsEligibilityTerminal
            => EligibilityOutcome == DocumentExchangeEligibilityOutcome.Denied;

        public bool IsReservationConfirmed
            => ReservationOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsReservationRejected
            => ReservationOutcome == ProviderOperationOutcome.Rejected;

        public bool IsDocumentExchangeConfirmed
            => DocumentExchangeOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsDocumentExchangeRejected
            => DocumentExchangeOutcome == ProviderOperationOutcome.Rejected;

        public IReadOnlyList<AcceptedExchangePlanCoupon> ReplacedCoupons
            => Coupons.Where(coupon => coupon.IsReplaced).ToList();

        public IReadOnlyList<long> ChangedOrderServiceIds
            => ReplacedCoupons.Select(coupon => coupon.PredecessorOrderServiceId).ToList();

        public bool CanReproduceDocumentRequest
            => Coupons.Count > 0 && Coupons.All(coupon => coupon.TicketedSegment.IsComplete);
    }
}
