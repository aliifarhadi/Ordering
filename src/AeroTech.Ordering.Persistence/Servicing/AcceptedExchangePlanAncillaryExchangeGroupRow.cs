using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanAncillaryExchangeGroupRow
    {
        public long OperationId { get; set; }

        public string ExchangeGroupRef { get; set; } = null!;

        public long SourceElectronicMiscDocumentId { get; set; }

        public string SourceDocumentNumber { get; set; } = null!;

        public string SourceCouponNumbers { get; set; } = null!;

        public ElectronicMiscDocumentType SuccessorType { get; set; }

        public string SuccessorReasonForIssuanceCode { get; set; } = null!;

        public int CurrencyId { get; set; }

        public string SuccessorCoupons { get; set; } = null!;

        public string DecisionReference { get; set; } = null!;

        public string SourceReference { get; set; } = null!;

        public PricingSource PricingSource { get; set; }

        public string? PricingLines { get; set; }

        public decimal? AddCollectAmount { get; set; }

        public int? AddCollectCurrencyId { get; set; }

        public decimal? RefundDueAmount { get; set; }

        public int? RefundDueCurrencyId { get; set; }

        public string? RefundDueDisposition { get; set; }

        public decimal? ResidualAmount { get; set; }

        public int? ResidualCurrencyId { get; set; }

        public string? ResidualDisposition { get; set; }

        public ResidualInstrumentKind? ResidualExpectedInstrument { get; set; }

        public ResidualFulfillment? ResidualFulfillment { get; set; }

        public string? FundingMethodRef { get; set; }

        public ProviderOperationOutcome? ExchangeOutcome { get; set; }

        public string? ExchangeProviderReference { get; set; }

        public string? ExchangeDetail { get; set; }

        public long? SuccessorElectronicMiscDocumentId { get; set; }

        public string? SuccessorDocumentNumber { get; set; }

        public long? PriceChangeSetId { get; set; }

        public ProviderOperationOutcome? FundingGuaranteeOutcome { get; set; }

        public string? FundingGuaranteeReference { get; set; }

        public string? FundingGuaranteeDetail { get; set; }

        public ProviderOperationOutcome? FundingCaptureOutcome { get; set; }

        public string? FundingCaptureReference { get; set; }

        public string? FundingCaptureDetail { get; set; }

        public ProviderOperationOutcome? RefundDueOutcome { get; set; }

        public string? RefundDueReference { get; set; }

        public string? RefundDueDetail { get; set; }

        public ProviderOperationOutcome? ResidualOutcome { get; set; }

        public string? ResidualProviderReference { get; set; }

        public string? ResidualInstrumentReference { get; set; }

        public ResidualInstrumentKind? ResidualInstrument { get; set; }

        public string? ResidualDetail { get; set; }
    }
}
