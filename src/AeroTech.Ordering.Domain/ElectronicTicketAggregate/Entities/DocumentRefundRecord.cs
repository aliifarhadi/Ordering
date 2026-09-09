using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class DocumentRefundRecord : Entity<long>
    {
        private readonly List<DocumentRefundCoupon> _coupons = new();

        private DocumentRefundRecord()
        {
        }

        internal DocumentRefundRecord(
            long id,
            long ticketId,
            long operationId,
            string quotedRefundId,
            string? sourcePricingReference,
            decimal approvedAmount,
            int currencyId,
            string approvedDisposition,
            string? dispositionReference,
            string? providerReference,
            long? refundedBy,
            string? actorScope,
            DateTimeOffset refundedAt)
        {
            Id = id;
            TicketId = ticketId;
            OperationId = operationId;
            QuotedRefundId = quotedRefundId;
            SourcePricingReference = sourcePricingReference;
            ApprovedAmount = approvedAmount;
            CurrencyId = currencyId;
            ApprovedDisposition = approvedDisposition;
            DispositionReference = dispositionReference;
            ProviderReference = providerReference;
            RefundedBy = refundedBy;
            ActorScope = actorScope;
            RefundedAt = refundedAt;
            ValueMovementStatus = ProviderOperationOutcome.Pending;
        }

        public long TicketId { get; private set; }

        public long OperationId { get; private set; }

        public string QuotedRefundId { get; private set; } = default!;

        public string? SourcePricingReference { get; private set; }

        public decimal ApprovedAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public string ApprovedDisposition { get; private set; } = default!;

        public string? DispositionReference { get; private set; }

        public string? ProviderReference { get; private set; }

        public long? RefundedBy { get; private set; }

        public string? ActorScope { get; private set; }

        public DateTimeOffset RefundedAt { get; private set; }

        public long? PriceChangeSetId { get; private set; }

        public ProviderOperationOutcome ValueMovementStatus { get; private set; }

        public string? ValueMovementReference { get; private set; }

        public string? ValueMovementDetail { get; private set; }

        public DateTimeOffset? ValueMovementUpdatedAt { get; private set; }

        public IReadOnlyCollection<DocumentRefundCoupon> Coupons => _coupons.AsReadOnly();

        public IReadOnlyCollection<long> RefundedOrderServiceIds()
            => _coupons.Select(coupon => coupon.OrderServiceId).Distinct().ToList();

        internal void AddCoupon(long id, long ticketCouponId, int couponNumber, long orderServiceId)
            => _coupons.Add(new DocumentRefundCoupon(id, Id, ticketCouponId, couponNumber, orderServiceId));

        internal void AttachPriceChangeSet(long priceChangeSetId) => PriceChangeSetId = priceChangeSetId;

        internal void RecordValueMovement(
            ProviderOperationOutcome outcome,
            string? reference,
            string? detail,
            DateTimeOffset observedAt)
        {
            ValueMovementStatus = outcome;
            ValueMovementReference = reference ?? ValueMovementReference;
            ValueMovementDetail = detail ?? ValueMovementDetail;
            ValueMovementUpdatedAt = observedAt;
        }
    }
}
