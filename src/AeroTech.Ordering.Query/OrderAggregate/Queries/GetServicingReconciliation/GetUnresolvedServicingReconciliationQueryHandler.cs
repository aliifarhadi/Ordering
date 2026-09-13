using AeroTech.Ordering.Query.OrderAggregate.View;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetServicingReconciliation
{
    public sealed class GetUnresolvedServicingReconciliationQueryHandler
        : IRequestHandler<GetUnresolvedServicingReconciliationQuery, IReadOnlyList<ServicingReconciliationView>>
    {
        private readonly ServicingReconciliationReader _reader;

        public GetUnresolvedServicingReconciliationQueryHandler(ServicingReconciliationReader reader)
            => _reader = reader;

        public Task<IReadOnlyList<ServicingReconciliationView>> Handle(
            GetUnresolvedServicingReconciliationQuery query,
            CancellationToken cancellationToken)
            => _reader.ListUnresolvedAsync(query.OrderId, cancellationToken);
    }
}
