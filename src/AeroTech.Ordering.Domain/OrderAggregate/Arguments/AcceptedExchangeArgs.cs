using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AcceptedExchangeArgs(
        AcceptedExchange Accepted,
        long PredecessorTravellerId,
        long SuccessorElectronicTicketId,
        IReadOnlyList<ExchangeCouponAllocation> Coupons,
        IReadOnlyDictionary<string, long> PredecessorPricingCorrelation,
        long OperationId,
        long? ActorId = null,
        string? ActorScope = null);
}
