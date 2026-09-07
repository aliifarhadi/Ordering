using System.ComponentModel.DataAnnotations;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class OperationOrderClaim
    {
        public long Id { get; set; }

        public long OperationId { get; set; }

        public long OrderId { get; set; }

        public long Generation { get; set; }

        public bool IsBlocking { get; set; }

        public DateTimeOffset AcquiredAt { get; set; }

        public DateTimeOffset RecoveryLeaseUntil { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;
    }
}
