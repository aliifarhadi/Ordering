using System.ComponentModel.DataAnnotations;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record QuoteExchange([property: Required] IReadOnlyList<long> ChangedOrderServiceIds);
}
