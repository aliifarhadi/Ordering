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
        string? FundingReleaseDetail = null,
        ProviderOperationOutcome? RefundDueOutcome = null,
        string? RefundDueReference = null,
        string? RefundDueDetail = null,
        ProviderOperationOutcome? ResidualOutcome = null,
        string? ResidualProviderReference = null,
        string? ResidualInstrumentReference = null,
        ResidualInstrumentKind? ResidualInstrument = null,
        string? ResidualDetail = null)
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

        public AcceptedRefundDue? RefundDue => Accepted.RefundDue;

        public AcceptedResidual? Residual => Accepted.Residual;

        public bool RequiresRefundDue => RefundDue is not null;

        public bool RequiresResidual => Residual is not null;

        public bool IsRefundDueSettled => RefundDueOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsRefundDueRejected => RefundDueOutcome == ProviderOperationOutcome.Rejected;

        public bool IsResidualSettled => ResidualOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsResidualRejected => ResidualOutcome == ProviderOperationOutcome.Rejected;

        public bool RequiresMonetarySettlement => RequiresFunding || RequiresRefundDue || RequiresResidual;

        public bool IsMonetarySettled
            => (!RequiresFunding || IsFundingCaptured)
               && (!RequiresRefundDue || IsRefundDueSettled)
               && (!RequiresResidual || IsResidualSettled);

        public bool IsCollectionSettled => !RequiresFunding || IsFundingCaptured;

        public bool RequiresReturnOfValue => RequiresRefundDue || RequiresResidual;

        public IReadOnlyList<AcceptedExchangeMonetaryLeg> MonetaryLegs => Accepted.MonetaryLegs();

        public bool CanReproduceRefundDueRequest => !RequiresRefundDue || RefundDue is not null;

        public bool CanReproduceResidualRequest => !RequiresResidual || Residual is not null;

        public decimal? MonetaryAmount => AddCollect?.Amount ?? RefundDue?.Amount ?? Residual?.Amount;

        public int? MonetaryCurrencyId => AddCollect?.CurrencyId ?? RefundDue?.CurrencyId ?? Residual?.CurrencyId;

        public string? MonetaryDisposition => RefundDue?.Disposition ?? Residual?.Disposition;

        public string? MonetaryProviderReference => MonetaryOutcome switch
        {
            ChangeMonetaryOutcome.AddCollect => FundingCaptureReference ?? FundingGuaranteeReference,
            ChangeMonetaryOutcome.Refund => RefundDueReference,
            ChangeMonetaryOutcome.Residual => ResidualProviderReference,
            _ => null
        };

        public ExchangeMonetaryState MonetaryState
        {
            get
            {
                var states = LegStates().ToList();

                if (states.Count == 0)
                    return ExchangeMonetaryState.NotRequired;

                if (states.Contains(ExchangeMonetaryState.Rejected))
                    return ExchangeMonetaryState.Rejected;

                if (states.Contains(ExchangeMonetaryState.Released))
                    return ExchangeMonetaryState.Released;

                if (states.Contains(ExchangeMonetaryState.Pending))
                    return ExchangeMonetaryState.Pending;

                if (states.Contains(ExchangeMonetaryState.Required))
                    return ExchangeMonetaryState.Required;

                return ExchangeMonetaryState.Settled;
            }
        }

        public ExchangeMonetaryState LegState(ExchangeMonetaryLegKind kind) => kind switch
        {
            ExchangeMonetaryLegKind.Collection => FundingMonetaryState(),
            ExchangeMonetaryLegKind.RefundDue => StateOf(RefundDueOutcome),
            _ => StateOf(ResidualOutcome)
        };

        public string? LegProviderReference(ExchangeMonetaryLegKind kind) => kind switch
        {
            ExchangeMonetaryLegKind.Collection => FundingCaptureReference ?? FundingGuaranteeReference,
            ExchangeMonetaryLegKind.RefundDue => RefundDueReference,
            _ => ResidualProviderReference
        };

        private IEnumerable<ExchangeMonetaryState> LegStates()
            => MonetaryLegs.Select(leg => LegState(leg.Kind));

        private ExchangeMonetaryState FundingMonetaryState() => FundingState switch
        {
            ExchangeFundingState.Captured => ExchangeMonetaryState.Settled,
            ExchangeFundingState.Released => ExchangeMonetaryState.Released,
            ExchangeFundingState.CaptureRejected or ExchangeFundingState.GuaranteeRejected
                => ExchangeMonetaryState.Rejected,
            ExchangeFundingState.GuaranteeRequired => ExchangeMonetaryState.Required,
            _ => ExchangeMonetaryState.Pending
        };

        private static ExchangeMonetaryState StateOf(ProviderOperationOutcome? outcome) => outcome switch
        {
            ProviderOperationOutcome.Confirmed => ExchangeMonetaryState.Settled,
            ProviderOperationOutcome.Rejected => ExchangeMonetaryState.Rejected,
            ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown => ExchangeMonetaryState.Pending,
            _ => ExchangeMonetaryState.Required
        };

        public bool RequiresFunding => AddCollect is not null;

        public bool IsFundingGuaranteed => FundingGuaranteeOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsFundingGuaranteeRejected => FundingGuaranteeOutcome == ProviderOperationOutcome.Rejected;

        public bool IsFundingCaptured => FundingCaptureOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsFundingCaptureRejected => FundingCaptureOutcome == ProviderOperationOutcome.Rejected;

        public bool IsFundingReleased => FundingReleaseOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsFundingReleaseRejected => FundingReleaseOutcome == ProviderOperationOutcome.Rejected;

        public bool IsFundingReleaseUnresolved
            => FundingReleaseOutcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown;

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
