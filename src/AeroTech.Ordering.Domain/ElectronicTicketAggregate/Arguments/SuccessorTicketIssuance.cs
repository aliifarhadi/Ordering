using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments
{
    public sealed record SuccessorTicketIssuance(
        long TicketId,
        long CouponId,
        long PredecessorTicketId,
        long PredecessorTicketCouponId,
        long ExchangeOperationId,
        long OrderId,
        long TravelerId,
        string DocumentNumber,
        int CouponNumber,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        DateTimeOffset? VoidDeadline,
        int CurrencyId,
        long OrderServiceId,
        long JourneySegmentId,
        IssuedSegmentSnapshot IssuedSegment,
        string? FareBasis,
        decimal IssuanceValue,
        IReadOnlyList<TicketCouponPriceLink> PriceLinks);
}
