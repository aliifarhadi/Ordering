using AeroTech.Ordering.Domain.Servicing.Plans.Contracts;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies;
using AeroTech.Ordering.Query.OrderAggregate.View;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetServicingReconciliation
{
    public sealed class ServicingReconciliationComposer
    {
        private readonly IServicingReconciliationStore _reconciliation;
        private readonly IServicingExternalEvidenceStore _evidence;
        private readonly IServicingManualResolutionStore _resolutions;
        private readonly IAcceptedExchangePlanStore _exchangePlans;

        public ServicingReconciliationComposer(
            IServicingReconciliationStore reconciliation,
            IServicingExternalEvidenceStore evidence,
            IServicingManualResolutionStore resolutions,
            IAcceptedExchangePlanStore exchangePlans)
        {
            _reconciliation = reconciliation;
            _evidence = evidence;
            _resolutions = resolutions;
            _exchangePlans = exchangePlans;
        }

        public async Task<ServicingReconciliationView> ComposeAsync(
            ServicingOperationSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            var evidence = await _evidence.ListAsync(snapshot.OperationId, cancellationToken);
            var documents = await _reconciliation.ListDocumentsAsync(snapshot.OrderId, cancellationToken);
            var control = await _reconciliation.ListControlAsync(snapshot.OrderId, cancellationToken);
            var reservations = await _reconciliation.ListReservationsAsync(snapshot.OrderId, cancellationToken);
            var resolutions = await _resolutions.ListAsync(snapshot.OperationId, cancellationToken);
            var plan = await _exchangePlans.FindAsync(snapshot.OperationId, cancellationToken);
            var manualReviews = ServicingRecoveryPolicy.ManualReviewReasons(plan);

            return new ServicingReconciliationView(
                snapshot.OperationId,
                snapshot.OrderId,
                snapshot.Kind,
                snapshot.Status,
                snapshot.ClaimGeneration,
                snapshot.ExpectedCommercialVersion,
                snapshot.CreatedAt,
                snapshot.UpdatedAt,
                snapshot.CallerScope,
                snapshot.IdempotencyKey,
                snapshot.ReceiptStatus,
                ServicingRecoveryPolicy.UnresolvedStage(snapshot, plan, evidence),
                evidence,
                documents,
                control,
                reservations,
                manualReviews,
                resolutions,
                ServicingRecoveryPolicy.Determine(snapshot, plan, evidence, manualReviews));
        }
    }
}
