using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.Ports.Refund;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Providers.Refund.Services
{
    public sealed class UnconfiguredRefundQuoteProvider : IRefundQuotePort
    {
        public Task<RefundQuote> QuoteAsync(RefundQuoteRequest request, CancellationToken cancellationToken = default)
            => throw ExceptionFactory.RefundQuoteSourceNotConfigured();

        public Task<AcceptedRefund> AcceptQuotedRefundAsync(
            AcceptedQuotedRefundSelection selection,
            CancellationToken cancellationToken = default)
            => throw ExceptionFactory.RefundQuoteSourceNotConfigured();
    }
}
