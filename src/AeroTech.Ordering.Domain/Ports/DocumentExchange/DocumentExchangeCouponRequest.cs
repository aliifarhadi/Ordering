using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record DocumentExchangeCouponRequest(
        int PredecessorCouponNumber,
        ExchangeCouponDisposition Disposition,
        TicketedSegmentSnapshot Segment);
}
