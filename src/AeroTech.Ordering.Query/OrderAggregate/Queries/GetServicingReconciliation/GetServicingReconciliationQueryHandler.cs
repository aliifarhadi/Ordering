using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using AeroTech.Ordering.Query.OrderAggregate.View;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetServicingReconciliation
{
    public sealed class GetServicingReconciliationQueryHandler
        : IRequestHandler<GetServicingReconciliationQuery, ServicingReconciliationView?>
    {
        private readonly IServicingReconciliationStore _operations;
        private readonly ServicingReconciliationComposer _composer;

        public GetServicingReconciliationQueryHandler(
            IServicingReconciliationStore operations,
            ServicingReconciliationComposer composer)
        {
            _operations = operations;
            _composer = composer;
        }

        public async Task<ServicingReconciliationView?> Handle(
            GetServicingReconciliationQuery query,
            CancellationToken cancellationToken)
            => await _operations.FindOperationAsync(query.OperationId, cancellationToken) is { } snapshot
                ? await _composer.ComposeAsync(snapshot, cancellationToken)
                : null;
    }
}
