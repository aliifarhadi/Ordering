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
        SuccessorDocumentIdentity? Successor = null,
        string? FundingMethodRef = null,
        ProviderOperationOutcome? FundingGuaranteeOutcome = null,
        string? FundingGuaranteeReference = null,
        string? FundingGuaranteeDetail = null,
        ProviderOperationOutcome? FundingCaptureOutcome = null,
        string? FundingCaptureReference = null,
        string? FundingCaptureDetail = null,
        ProviderOperationOutcome? FundingReleaseOutcome = null,
        string? FundingReleaseDetail = null)
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

        public AcceptedAddCollect? AddCollect => Accepted.AddCollect;

        public bool RequiresFunding => MonetaryOutcome == ChangeMonetaryOutcome.AddCollect;

        public bool IsFundingGuaranteed => FundingGuaranteeOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsFundingGuaranteeRejected => FundingGuaranteeOutcome == ProviderOperationOutcome.Rejected;

        public bool IsFundingCaptured => FundingCaptureOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsFundingCaptureRejected => FundingCaptureOutcome == ProviderOperationOutcome.Rejected;

        public bool IsFundingReleased => FundingReleaseOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsFundingAssured => !RequiresFunding || IsFundingGuaranteed;

        public bool IsFundingSettled => !RequiresFunding || IsFundingCaptured;

        public bool CanReproduceFundingRequest
            => !RequiresFunding || (AddCollect is not null && !string.IsNullOrWhiteSpace(FundingMethodRef));

        public ExchangeFundingState FundingState => !RequiresFunding
            ? ExchangeFundingState.NotRequired
            : FundingReleaseOutcome switch
            {
                ProviderOperationOutcome.Confirmed => ExchangeFundingState.Released,
                ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown
                    => ExchangeFundingState.ReleasePending,
                _ => CaptureState()
            };

        private ExchangeFundingState CaptureState() => FundingCaptureOutcome switch
        {
            ProviderOperationOutcome.Confirmed => ExchangeFundingState.Captured,
            ProviderOperationOutcome.Rejected => ExchangeFundingState.CaptureRejected,
            ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown
                => ExchangeFundingState.CapturePending,
            _ => GuaranteeState()
        };

        private ExchangeFundingState GuaranteeState() => FundingGuaranteeOutcome switch
        {
            ProviderOperationOutcome.Confirmed => ExchangeFundingState.Guaranteed,
            ProviderOperationOutcome.Rejected => ExchangeFundingState.GuaranteeRejected,
            ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown
                => ExchangeFundingState.GuaranteePending,
            _ => ExchangeFundingState.GuaranteeRequired
        };
    }
}
