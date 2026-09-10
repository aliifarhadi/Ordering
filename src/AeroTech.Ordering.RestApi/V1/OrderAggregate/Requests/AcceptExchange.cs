using System.ComponentModel.DataAnnotations;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record AcceptExchange(
        long PredecessorOrderServiceId,
        [property: Required] string QuotedExchangeId);
}
