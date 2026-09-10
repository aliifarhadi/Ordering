using System.ComponentModel.DataAnnotations;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record AcceptExchange(
        [property: Required] IReadOnlyList<long> ChangedOrderServiceIds,
        [property: Required] string QuotedExchangeId);
}
