using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class CommandReceiptReadModel
    {
        public long Id { get; set; }

        public string CallerScope { get; set; } = null!;

        public string IdempotencyKey { get; set; } = null!;

        public CommandReceiptStatus Status { get; set; }
    }
}
