using System.ComponentModel.DataAnnotations;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record CancelOrderItem(
        long OrderItemId,
        [property: Required] string QuotedCancellationId);
}
