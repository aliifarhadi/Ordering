using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.QuoteRefund
{
    public sealed record QuoteRefundQuery(
        long OrderId,
        long ElectronicTicketId,
        IReadOnlyList<long>? TicketCouponIds = null) : IRequest<RefundQuoteOutcome>;
}
