using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.DocumentStockAggregate.Entities
{
    public sealed class DocumentStockAllocation : Entity<long>
    {
        private DocumentStockAllocation()
        {
        }

        internal DocumentStockAllocation(
            long id,
            long documentStockId,
            long operationId,
            string documentRole,
            long serial,
            string documentNumber,
            DateTimeOffset allocatedAt)
        {
            Id = id;
            DocumentStockId = documentStockId;
            OperationId = operationId;
            DocumentRole = documentRole;
            Serial = serial;
            DocumentNumber = documentNumber;
            State = StockNumberState.Reserved;
            AllocatedAt = allocatedAt;
        }

        public long DocumentStockId { get; private set; }

        public long OperationId { get; private set; }

        public string DocumentRole { get; private set; } = default!;

        public long Serial { get; private set; }

        public string DocumentNumber { get; private set; } = default!;

        public StockNumberState State { get; private set; }

        public DateTimeOffset AllocatedAt { get; private set; }

        public DateTimeOffset? SettledAt { get; private set; }

        internal void MarkIssued(DateTimeOffset at)
        {
            State = StockNumberState.Issued;
            SettledAt = at;
        }

        internal void Retire(DateTimeOffset at)
        {
            State = StockNumberState.Retired;
            SettledAt = at;
        }
    }
}
