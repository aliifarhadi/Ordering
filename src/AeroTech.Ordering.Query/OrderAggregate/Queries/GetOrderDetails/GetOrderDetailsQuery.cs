using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails
{
    public sealed record GetOrderDetailsQuery(long OrderId) : IRequest<OrderDetailsDto?>;

    public sealed record OrderDetailsDto(long OrderId, long ProjectionRevision, DateTimeOffset UpdatedAt, object Snapshot);
}
