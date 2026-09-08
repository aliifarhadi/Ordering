namespace AeroTech.Ordering.Application.OrderAggregate.Operations
{
    public sealed class OrderOperationOptions
    {
        public const string SectionName = "OrderOperations";

        public int? RecoveryLeaseSeconds { get; set; }

        public string? TicketDocumentType { get; set; }

        public string? EmdDocumentType { get; set; }
    }
}
