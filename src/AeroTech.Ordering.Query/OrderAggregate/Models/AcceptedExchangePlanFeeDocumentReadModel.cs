using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class AcceptedExchangePlanFeeDocumentReadModel
    {
        public long OperationId { get; set; }

        public string DocumentReference { get; set; } = null!;

        public ProviderOperationOutcome? IssuanceOutcome { get; set; }

        public DateTimeOffset? SettledAt { get; set; }
    }
}
