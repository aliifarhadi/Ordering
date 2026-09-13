using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class ServicingExternalEvidenceReadModel
    {
        public long OperationId { get; set; }

        public ServicingEvidenceStage Stage { get; set; }

        public ProviderOperationOutcome Outcome { get; set; }

        public string? ProviderReference { get; set; }

        public string? Detail { get; set; }

        public AccountableDocumentKind? DocumentKind { get; set; }

        public string? DocumentNumber { get; set; }

        public DateTimeOffset RecordedAt { get; set; }
    }
}
