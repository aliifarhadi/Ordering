using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class AcceptedExchangePlanAncillaryExchangeGroupReadModel
    {
        public long OperationId { get; set; }

        public string ExchangeGroupRef { get; set; } = null!;

        public decimal? AddCollectAmount { get; set; }

        public int? AddCollectCurrencyId { get; set; }

        public decimal? RefundDueAmount { get; set; }

        public int? RefundDueCurrencyId { get; set; }

        public decimal? ResidualAmount { get; set; }

        public int? ResidualCurrencyId { get; set; }

        public ResidualFulfillment? ResidualFulfillment { get; set; }

        public ProviderOperationOutcome? ExchangeOutcome { get; set; }

        public long? SuccessorElectronicMiscDocumentId { get; set; }

        public ProviderOperationOutcome? FundingCaptureOutcome { get; set; }

        public ProviderOperationOutcome? RefundDueOutcome { get; set; }

        public ProviderOperationOutcome? ResidualOutcome { get; set; }
    }
}
