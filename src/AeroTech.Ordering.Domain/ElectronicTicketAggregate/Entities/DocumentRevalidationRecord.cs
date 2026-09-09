using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class DocumentRevalidationRecord : Entity<long>
    {
        private DocumentRevalidationRecord()
        {
        }

        internal DocumentRevalidationRecord(
            long id,
            long ticketId,
            long operationId,
            string quotedChangeId,
            string targetSelectionRef,
            long ticketCouponId,
            int couponNumber,
            long previousOrderServiceId,
            long newOrderServiceId,
            string? providerReference,
            long? revalidatedBy,
            string? actorScope,
            DateTimeOffset revalidatedAt)
        {
            Id = id;
            TicketId = ticketId;
            OperationId = operationId;
            QuotedChangeId = quotedChangeId;
            TargetSelectionRef = targetSelectionRef;
            TicketCouponId = ticketCouponId;
            CouponNumber = couponNumber;
            PreviousOrderServiceId = previousOrderServiceId;
            NewOrderServiceId = newOrderServiceId;
            ProviderReference = providerReference;
            RevalidatedBy = revalidatedBy;
            ActorScope = actorScope;
            RevalidatedAt = revalidatedAt;
        }

        public long TicketId { get; private set; }

        public long OperationId { get; private set; }

        public string QuotedChangeId { get; private set; } = default!;

        public string TargetSelectionRef { get; private set; } = default!;

        public long TicketCouponId { get; private set; }

        public int CouponNumber { get; private set; }

        public long PreviousOrderServiceId { get; private set; }

        public long NewOrderServiceId { get; private set; }

        public string? ProviderReference { get; private set; }

        public long? RevalidatedBy { get; private set; }

        public string? ActorScope { get; private set; }

        public DateTimeOffset RevalidatedAt { get; private set; }
    }
}
