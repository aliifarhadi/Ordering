using AeroTech.Ordering.Domain.TrafficDocumentAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities
{
    // Structure only for now — EMD issuance is a later phase (no factory flow wired yet).
    public sealed class EmdDocument : TrafficDocument
    {
        private EmdDocument()
        {
        }

        private EmdDocument(
            long id,
            long orderId,
            long travellerId,
            string documentNumber,
            EmdType emdType,
            string? reasonForIssuanceCode,
            SalesChannel issueChannel,
            long? issueUserId,
            string? issueReference,
            DocumentAmounts amounts,
            DateTimeOffset issuedAt,
            DateTimeOffset validUntil,
            DateTimeOffset? voidDeadline)
            : base(id, orderId, travellerId, documentNumber, issueChannel, issueUserId, issueReference, amounts, issuedAt, validUntil, voidDeadline)
        {
            EmdType = emdType;
            ReasonForIssuanceCode = reasonForIssuanceCode;
        }

        public EmdType EmdType { get; private set; }

        public string? ReasonForIssuanceCode { get; private set; }

        public static EmdDocument Create(
            long id,
            long orderId,
            long travellerId,
            string documentNumber,
            EmdType emdType,
            string? reasonForIssuanceCode,
            SalesChannel issueChannel,
            long? issueUserId,
            string? issueReference,
            DocumentAmounts amounts,
            DateTimeOffset issuedAt,
            DateTimeOffset validUntil,
            DateTimeOffset? voidDeadline)
            => new(id, orderId, travellerId, documentNumber, emdType, reasonForIssuanceCode, issueChannel, issueUserId, issueReference, amounts, issuedAt, validUntil, voidDeadline);

        public EmdCoupon AddCoupon(long id, long orderServiceId, long? associatedTicketCouponId, int couponNumber, DocumentAmounts amounts)
        {
            var coupon = new EmdCoupon(id, Id, orderServiceId, associatedTicketCouponId, couponNumber, amounts);
            RegisterCoupon(coupon);
            return coupon;
        }
    }
}
