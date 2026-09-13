using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using AeroTech.Ordering.Query.OrderAggregate.View;
using MediatR;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetServicingReconciliation
{
    public sealed class GetUnresolvedServicingReconciliationQueryHandler
        : IRequestHandler<GetUnresolvedServicingReconciliationQuery, IReadOnlyList<ServicingReconciliationView>>
    {
        private readonly IServicingReconciliationStore _operations;
        private readonly ServicingReconciliationComposer _composer;

        public GetUnresolvedServicingReconciliationQueryHandler(
            IServicingReconciliationStore operations,
            ServicingReconciliationComposer composer)
        {
            _operations = operations;
            _composer = composer;
        }

        public async Task<IReadOnlyList<ServicingReconciliationView>> Handle(
            GetUnresolvedServicingReconciliationQuery query,
            CancellationToken cancellationToken)
        {
            var views = new List<ServicingReconciliationView>();

            foreach (var snapshot in await _operations.ListUnresolvedAsync(query.OrderId, cancellationToken))
                views.Add(await _composer.ComposeAsync(snapshot, cancellationToken));

            return views;
        }
    }
}
