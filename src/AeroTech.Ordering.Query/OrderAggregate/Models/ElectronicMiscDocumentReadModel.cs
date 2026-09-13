using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class ElectronicMiscDocumentReadModel
    {
        public long Id { get; set; }

        public long? CurrentServicingOrderId { get; set; }

        public string DocumentNumber { get; set; } = null!;

        public ElectronicMiscDocumentStatus StatusSummary { get; set; }

        public int DocumentVersion { get; set; }

        public string? ProviderReference { get; set; }
    }
}
