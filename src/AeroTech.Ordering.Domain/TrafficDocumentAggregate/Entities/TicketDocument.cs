using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities
{
    public sealed class TicketDocument : TrafficDocument
    {
        private TicketDocument()
        {
        }

        private TicketDocument(
            long id,
            long orderId,
            long travellerId,
            string ticketNumber,
            SalesChannel issueChannel,
            long? issueUserId,
            string? issueReference,
            DocumentAmounts amounts,
            DateTimeOffset issuedAt,
            DateTimeOffset validUntil,
            DateTimeOffset? voidDeadline)
            : base(id, orderId, travellerId, ticketNumber, issueChannel, issueUserId, issueReference, amounts, issuedAt, validUntil, voidDeadline)
        {
        }

        public static TicketDocument Create(
            long id,
            long orderId,
            long travellerId,
            string ticketNumber,
            SalesChannel issueChannel,
            long? issueUserId,
            string? issueReference,
            DocumentAmounts amounts,
            DateTimeOffset issuedAt,
            DateTimeOffset validUntil,
            DateTimeOffset? voidDeadline)
            => new(id, orderId, travellerId, ticketNumber, issueChannel, issueUserId, issueReference, amounts, issuedAt, validUntil, voidDeadline);

        public TicketCoupon AddCoupon(long id, long orderServiceId, long orderSegmentId, int couponNumber, DocumentAmounts amounts)
        {
            var coupon = new TicketCoupon(id, Id, orderServiceId, orderSegmentId, couponNumber, amounts);
            RegisterCoupon(coupon);
            return coupon;
        }
    }
}
