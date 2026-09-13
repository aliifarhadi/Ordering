using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class ElectronicTicketReadModel
    {
        public long Id { get; set; }

        public long? CurrentServicingOrderId { get; set; }

        public string DocumentNumber { get; set; } = null!;

        public ElectronicTicketStatus StatusSummary { get; set; }

        public int DocumentVersion { get; set; }

        public long? PredecessorElectronicTicketId { get; set; }

        public string? ProviderReference { get; set; }
    }
}
