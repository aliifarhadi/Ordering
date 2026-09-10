using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Queries.QuoteRefund
{
    public sealed class QuoteRefundQueryHandler : IRequestHandler<QuoteRefundQuery, RefundQuoteOutcome>
    {
        private readonly IRefundService _refundService;

        public QuoteRefundQueryHandler(IRefundService refundService) => _refundService = refundService;

        public Task<RefundQuoteOutcome> Handle(QuoteRefundQuery query, CancellationToken cancellationToken)
            => _refundService.QuoteAsync(query.OrderId, query.ElectronicTicketId, query.TicketCouponIds, cancellationToken);
    }
}
