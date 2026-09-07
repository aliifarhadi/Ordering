using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class CommandReceipt
    {
        public long Id { get; set; }

        public long OwnerAirlineId { get; set; }

        public string CallerScope { get; set; } = null!;

        public string OperationName { get; set; } = null!;

        public string IdempotencyKey { get; set; } = null!;

        public string RequestHash { get; set; } = null!;

        public string? PayloadRef { get; set; }

        public long? OrderId { get; set; }

        public long? OperationId { get; set; }

        public CommandReceiptStatus Status { get; set; }

        public string? ResultRef { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
