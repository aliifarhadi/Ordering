using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal sealed record ExchangeScenario(
        long OrderId,
        long ServiceId,
        long TicketId,
        long CouponId,
        int CommercialVersion,
        long FinancialSequence,
        long ObligationVersion,
        decimal CustomerTotal,
        int DocumentVersion,
        AcceptedExchange Accepted)
    {
        public ExchangeExecution Execution(string key)
            => new(OrderId, ServiceId, ExchangeSourceFactory.QuoteId, key, CommercialVersion);
    }
}
