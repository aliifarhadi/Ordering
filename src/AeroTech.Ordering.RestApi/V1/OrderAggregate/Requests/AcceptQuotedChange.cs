using System.ComponentModel.DataAnnotations;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record AcceptQuotedChange(
        long OrderServiceId,
        [property: Required] string QuotedChangeId);
}
