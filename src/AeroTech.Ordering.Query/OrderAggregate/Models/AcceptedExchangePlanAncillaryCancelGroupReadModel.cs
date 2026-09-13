using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class AcceptedExchangePlanAncillaryCancelGroupReadModel
    {
        public long OperationId { get; set; }

        public string CancelGroupRef { get; set; } = null!;

        public EligibilityOutcome? VoidEligibilityOutcome { get; set; }

        public bool VoidRefundRequiredInstead { get; set; }

        public ProviderOperationOutcome? VoidOutcome { get; set; }

        public DateTimeOffset? CancellationSettledAt { get; set; }
    }
}
