using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class DocumentRefundCorrectionRecord : Entity<long>
    {
        private readonly List<DocumentRefundCorrectionCoupon> _coupons = new();

        private DocumentRefundCorrectionRecord()
        {
        }

        internal DocumentRefundCorrectionRecord(
            long id,
            long ticketId,
            long documentRefundRecordId,
            long operationId,
            long originalRefundOperationId,
            decimal correctedAmount,
            int currencyId,
            string reason,
            string? reasonDetail,
            string? providerReference,
            long? correctedBy,
            string? actorScope,
            DateTimeOffset correctedAt)
        {
            Id = id;
            TicketId = ticketId;
            DocumentRefundRecordId = documentRefundRecordId;
            OperationId = operationId;
            OriginalRefundOperationId = originalRefundOperationId;
            CorrectedAmount = correctedAmount;
            CurrencyId = currencyId;
            Reason = reason;
            ReasonDetail = reasonDetail;
            ProviderReference = providerReference;
            CorrectedBy = correctedBy;
            ActorScope = actorScope;
            CorrectedAt = correctedAt;
            ValueCorrectionStatus = ProviderOperationOutcome.Pending;
        }

        public long TicketId { get; private set; }

        public long DocumentRefundRecordId { get; private set; }

        public long OperationId { get; private set; }

        public long OriginalRefundOperationId { get; private set; }

        public decimal CorrectedAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public string Reason { get; private set; } = default!;

        public string? ReasonDetail { get; private set; }

        public string? ProviderReference { get; private set; }

        public long? CorrectedBy { get; private set; }

        public string? ActorScope { get; private set; }

        public DateTimeOffset CorrectedAt { get; private set; }

        public long? PriceChangeSetId { get; private set; }

        public ProviderOperationOutcome ValueCorrectionStatus { get; private set; }

        public string? ValueCorrectionReference { get; private set; }

        public string? ValueCorrectionDetail { get; private set; }

        public DateTimeOffset? ValueCorrectionUpdatedAt { get; private set; }

        public IReadOnlyCollection<DocumentRefundCorrectionCoupon> Coupons => _coupons.AsReadOnly();

        public IReadOnlyCollection<long> RestoredOrderServiceIds()
            => _coupons.Select(coupon => coupon.OrderServiceId).Distinct().ToList();

        internal void AddCoupon(long id, long ticketCouponId, int couponNumber, long orderServiceId)
            => _coupons.Add(new DocumentRefundCorrectionCoupon(id, Id, ticketCouponId, couponNumber, orderServiceId));

        internal void AttachPriceChangeSet(long priceChangeSetId) => PriceChangeSetId = priceChangeSetId;

        internal void RecordValueCorrection(
            ProviderOperationOutcome outcome,
            string? reference,
            string? detail,
            DateTimeOffset observedAt)
        {
            ValueCorrectionStatus = outcome;
            ValueCorrectionReference = reference ?? ValueCorrectionReference;
            ValueCorrectionDetail = detail ?? ValueCorrectionDetail;
            ValueCorrectionUpdatedAt = observedAt;
        }
    }
}
