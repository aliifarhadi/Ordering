using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;

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
            PricingSource pricingSource,
            string? sourcePricingReference,
            string? sourceRefundType,
            string? sourceEvidence,
            ManualRefundAuthority? manualAuthority,
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
            PricingSource = pricingSource;
            SourcePricingReference = sourcePricingReference;
            SourceRefundType = sourceRefundType;
            SourceEvidence = sourceEvidence;
            ManualAuthority = manualAuthority;
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

        public PricingSource PricingSource { get; private set; }

        public string? SourcePricingReference { get; private set; }

        public string? SourceRefundType { get; private set; }

        public string? SourceEvidence { get; private set; }

        public ManualRefundAuthority? ManualAuthority { get; private set; }

        public bool IsManual => PricingSource == PricingSource.Manual;

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
