using System.ComponentModel.DataAnnotations;

namespace AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests
{
    public sealed record RemoveOrderServices(
        [property: Required] IReadOnlyList<long> OrderServiceIds,
        [property: Required] string QuotedCancellationId);
}
