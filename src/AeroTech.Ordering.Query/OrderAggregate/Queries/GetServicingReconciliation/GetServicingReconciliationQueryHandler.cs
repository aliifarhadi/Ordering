using AeroTech.Ordering.Query.OrderAggregate.View;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetServicingReconciliation
{
    public sealed class GetServicingReconciliationQueryHandler
        : IRequestHandler<GetServicingReconciliationQuery, ServicingReconciliationView?>
    {
        private readonly ServicingReconciliationReader _reader;

        public GetServicingReconciliationQueryHandler(ServicingReconciliationReader reader)
            => _reader = reader;

        public Task<ServicingReconciliationView?> Handle(
            GetServicingReconciliationQuery query,
            CancellationToken cancellationToken)
            => _reader.FindAsync(query.OperationId, cancellationToken);
    }
}
