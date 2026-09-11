using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities
{
    public sealed class EmdCouponAssociationChange : Entity<long>
    {
        private EmdCouponAssociationChange()
        {
        }

        internal EmdCouponAssociationChange(
            long id,
            long emdCouponId,
            int sequence,
            EmdCouponAssociationChangeKind kind,
            long? previousTicketCouponId,
            string? previousDocumentNumber,
            int? previousCouponNumber,
            long? currentTicketCouponId,
            string? currentDocumentNumber,
            int? currentCouponNumber,
            long? operationId,
            string? decisionReference,
            string? providerReference,
            DateTimeOffset changedAt)
        {
            Id = id;
            EmdCouponId = emdCouponId;
            Sequence = sequence;
            Kind = kind;
            PreviousTicketCouponId = previousTicketCouponId;
            PreviousDocumentNumber = previousDocumentNumber;
            PreviousCouponNumber = previousCouponNumber;
            CurrentTicketCouponId = currentTicketCouponId;
            CurrentDocumentNumber = currentDocumentNumber;
            CurrentCouponNumber = currentCouponNumber;
            OperationId = operationId;
            DecisionReference = decisionReference;
            ProviderReference = providerReference;
            ChangedAt = changedAt;
        }

        public long EmdCouponId { get; private set; }

        public int Sequence { get; private set; }

        public EmdCouponAssociationChangeKind Kind { get; private set; }

        public long? PreviousTicketCouponId { get; private set; }

        public string? PreviousDocumentNumber { get; private set; }

        public int? PreviousCouponNumber { get; private set; }

        public long? CurrentTicketCouponId { get; private set; }

        public string? CurrentDocumentNumber { get; private set; }

        public int? CurrentCouponNumber { get; private set; }

        public long? OperationId { get; private set; }

        public string? DecisionReference { get; private set; }

        public string? ProviderReference { get; private set; }

        public DateTimeOffset ChangedAt { get; private set; }
    }
}
