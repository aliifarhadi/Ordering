using AeroTech.Ordering.Query.OrderAggregate.View;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetServicingReconciliation
{
    public sealed record GetUnresolvedServicingReconciliationQuery(long OrderId)
        : IRequest<IReadOnlyList<ServicingReconciliationView>>;
}
