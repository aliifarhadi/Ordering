using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class DocumentExchangeRecord : Entity<long>
    {
        private readonly List<DocumentExchangeCoupon> _coupons = new();

        private DocumentExchangeRecord()
        {
        }

        internal DocumentExchangeRecord(
            long id,
            long predecessorElectronicTicketId,
            long successorElectronicTicketId,
            string successorDocumentNumber,
            long operationId,
            string quotedExchangeId,
            string targetSelectionRef,
            string? sourcePricingReference,
            string? providerReference,
            long? actorId,
            string? actorScope,
            DateTimeOffset exchangedAt)
        {
            Id = id;
            PredecessorElectronicTicketId = predecessorElectronicTicketId;
            SuccessorElectronicTicketId = successorElectronicTicketId;
            SuccessorDocumentNumber = successorDocumentNumber;
            OperationId = operationId;
            QuotedExchangeId = quotedExchangeId;
            TargetSelectionRef = targetSelectionRef;
            SourcePricingReference = sourcePricingReference;
            ProviderReference = providerReference;
            ActorId = actorId;
            ActorScope = actorScope;
            ExchangedAt = exchangedAt;
        }

        public long PredecessorElectronicTicketId { get; private set; }

        public long SuccessorElectronicTicketId { get; private set; }

        public string SuccessorDocumentNumber { get; private set; } = default!;

        public long OperationId { get; private set; }

        public string QuotedExchangeId { get; private set; } = default!;

        public string TargetSelectionRef { get; private set; } = default!;

        public string? SourcePricingReference { get; private set; }

        public string? ProviderReference { get; private set; }

        public long? ActorId { get; private set; }

        public string? ActorScope { get; private set; }

        public DateTimeOffset ExchangedAt { get; private set; }

        public IReadOnlyCollection<DocumentExchangeCoupon> Coupons => _coupons.AsReadOnly();

        internal void AddCoupon(
            long id,
            long predecessorTicketCouponId,
            int predecessorCouponNumber,
            long successorTicketCouponId,
            int successorCouponNumber,
            long previousOrderServiceId,
            long successorOrderServiceId)
            => _coupons.Add(new DocumentExchangeCoupon(
                id,
                Id,
                predecessorTicketCouponId,
                predecessorCouponNumber,
                successorTicketCouponId,
                successorCouponNumber,
                previousOrderServiceId,
                successorOrderServiceId));
    }
}
