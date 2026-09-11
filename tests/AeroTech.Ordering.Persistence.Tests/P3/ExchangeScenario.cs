using AeroTech.Ordering.Application.OrderAggregate.Services.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal sealed record ExchangeScenario(
        long OrderId,
        IReadOnlyList<long> ChangedOrderServiceIds,
        long TicketId,
        IReadOnlyDictionary<int, long> CouponIds,
        IReadOnlyDictionary<int, long> CouponServiceIds,
        int CommercialVersion,
        long FinancialSequence,
        long ObligationVersion,
        decimal CustomerTotal,
        int DocumentVersion,
        AcceptedExchange Accepted)
    {
        public long ServiceId => ChangedOrderServiceIds[0];

        public long CouponId => CouponIds[CouponServiceIds.Single(pair => pair.Value == ServiceId).Key];

        public ExchangeExecution Execution(string key)
            => new(OrderId, ChangedOrderServiceIds, ExchangeSourceFactory.QuoteId, key, CommercialVersion);

        public ExchangeExecution FundedExecution(string key, string? fundingMethodRef = ExchangeSourceFactory.FundingMethodRef)
            => Execution(key) with { FundingMethodRef = fundingMethodRef };
    }
}
