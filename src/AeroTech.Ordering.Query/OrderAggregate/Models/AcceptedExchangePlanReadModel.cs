using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class AcceptedExchangePlanReadModel
    {
        public long OperationId { get; set; }

        public string AcceptedPlan { get; set; } = null!;

        public DocumentExchangeEligibilityOutcome? EligibilityOutcome { get; set; }

        public ProviderOperationOutcome ReservationOutcome { get; set; }

        public ProviderOperationOutcome? DocumentExchangeOutcome { get; set; }

        public ProviderOperationOutcome? FundingCaptureOutcome { get; set; }

        public ProviderOperationOutcome? RefundDueOutcome { get; set; }

        public ProviderOperationOutcome? ResidualOutcome { get; set; }
    }
}
