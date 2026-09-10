using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments
{
    public sealed record SuccessorTicketIssuance(
        long TicketId,
        long PredecessorTicketId,
        long ExchangeOperationId,
        long OrderId,
        long TravelerId,
        string DocumentNumber,
        long IssuerCarrierId,
        long? IssuingOfficeId,
        DocumentAuthority Authority,
        DateTimeOffset? VoidDeadline,
        int CurrencyId,
        IReadOnlyList<SuccessorCouponIssuance> Coupons);
}
