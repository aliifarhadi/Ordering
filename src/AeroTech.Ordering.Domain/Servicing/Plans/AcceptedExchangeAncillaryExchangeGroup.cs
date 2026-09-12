using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record AcceptedExchangeAncillaryExchangeGroup(
        string ExchangeGroupRef,
        long SourceElectronicMiscDocumentId,
        string SourceDocumentNumber,
        IReadOnlyList<int> SourceCouponNumbers,
        ElectronicMiscDocumentType SuccessorType,
        string SuccessorReasonForIssuanceCode,
        int CurrencyId,
        IReadOnlyList<AcceptedExchangeAncillarySuccessorCoupon> SuccessorCoupons,
        string DecisionReference,
        string SourceReference,
        PricingSource PricingSource,
        IReadOnlyList<AcceptedRefundPricingLine> PricingLines,
        AcceptedAddCollect? AddCollect = null,
        AcceptedRefundDue? RefundDue = null,
        AcceptedResidual? Residual = null,
        string? FundingMethodRef = null,
        ProviderOperationOutcome? ExchangeOutcome = null,
        string? ExchangeProviderReference = null,
        string? ExchangeDetail = null,
        long? SuccessorElectronicMiscDocumentId = null,
        string? SuccessorDocumentNumber = null,
        long? PriceChangeSetId = null,
        ProviderOperationOutcome? FundingGuaranteeOutcome = null,
        string? FundingGuaranteeReference = null,
        string? FundingGuaranteeDetail = null,
        ProviderOperationOutcome? FundingCaptureOutcome = null,
        string? FundingCaptureReference = null,
        string? FundingCaptureDetail = null,
        ProviderOperationOutcome? RefundDueOutcome = null,
        string? RefundDueReference = null,
        string? RefundDueDetail = null,
        ProviderOperationOutcome? ResidualOutcome = null,
        string? ResidualProviderReference = null,
        string? ResidualInstrumentReference = null,
        ResidualInstrumentKind? ResidualInstrument = null,
        string? ResidualDetail = null)
    {
        public const string LegPrefix = "emd-exchange";

        public const string FundingGuaranteeLegPrefix = "emd-exchange-guarantee";

        public const string FundingCaptureLegPrefix = "emd-exchange-capture";

        public const string RefundDueLegPrefix = "emd-exchange-refund";

        public const string ResidualLegPrefix = "emd-exchange-residual";

        public string LegIdentity => $"{LegPrefix}:{ExchangeGroupRef}";

        public string FundingGuaranteeLegIdentity => $"{FundingGuaranteeLegPrefix}:{ExchangeGroupRef}";

        public string FundingCaptureLegIdentity => $"{FundingCaptureLegPrefix}:{ExchangeGroupRef}";

        public string RefundDueLegIdentity => $"{RefundDueLegPrefix}:{ExchangeGroupRef}";

        public string ResidualLegIdentity => $"{ResidualLegPrefix}:{ExchangeGroupRef}";

        public bool IsAssociatedSuccessor => SuccessorType == ElectronicMiscDocumentType.Associated;

        public bool RequiresFunding => AddCollect is not null;

        public bool RequiresRefundDue => RefundDue is not null;

        public bool IsRefundDueSettled => RefundDueOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsRefundDueRejected => RefundDueOutcome == ProviderOperationOutcome.Rejected;

        public bool RequiresExternalResidual => Residual is { IsDocumentCoupled: false };

        public bool RequiresDocumentCoupledResidual => Residual is { IsDocumentCoupled: true };

        public bool IsFundingGuaranteed => FundingGuaranteeOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsFundingCaptured => FundingCaptureOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsExchangeConfirmed => ExchangeOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsExchangeRejected => ExchangeOutcome == ProviderOperationOutcome.Rejected;

        public bool IsResidualSettled => ResidualOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsConsequenceCommitted => PriceChangeSetId is not null;

        public bool IsMaterialized => SuccessorElectronicMiscDocumentId is not null;

        public bool IsSettled => IsExchangeConfirmed
                                 && IsMaterialized
                                 && (!RequiresFunding || IsFundingCaptured)
                                 && (!RequiresRefundDue || IsRefundDueSettled)
                                 && (!RequiresExternalResidual || IsResidualSettled);

        public bool IsRejected => IsExchangeRejected
                                  || FundingGuaranteeOutcome == ProviderOperationOutcome.Rejected
                                  || FundingCaptureOutcome == ProviderOperationOutcome.Rejected
                                  || IsRefundDueRejected
                                  || ResidualOutcome == ProviderOperationOutcome.Rejected;

        public ExchangeAncillaryState State => ExchangeOutcome switch
        {
            null => ExchangeAncillaryState.NotStarted,
            ProviderOperationOutcome.Rejected => ExchangeAncillaryState.Rejected,
            ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown
                => ExchangeAncillaryState.Pending,
            _ => IsRejected
                ? ExchangeAncillaryState.Rejected
                : IsSettled
                    ? ExchangeAncillaryState.Confirmed
                    : ExchangeAncillaryState.Pending
        };
    }
}
