using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class ServicingManualResolutionReadModel
    {
        public long OperationId { get; set; }

        public string ResolutionId { get; set; } = null!;

        public ServicingResolutionKind Kind { get; set; }

        public string Actor { get; set; } = null!;

        public string Reason { get; set; } = null!;

        public string? Reference { get; set; }

        public ServicingEvidenceStage? EvidenceStage { get; set; }

        public long ExpectedClaimGeneration { get; set; }

        public DateTimeOffset RecordedAt { get; set; }
    }
}
