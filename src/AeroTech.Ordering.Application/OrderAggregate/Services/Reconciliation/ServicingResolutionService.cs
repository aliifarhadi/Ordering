using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.Servicing.Plans.Contracts;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reconciliation
{
    public sealed class ServicingResolutionService : IServicingResolutionService
    {
        private readonly IServicingReconciliationStore _reconciliation;
        private readonly IServicingExternalEvidenceStore _evidence;
        private readonly IServicingManualResolutionStore _resolutions;
        private readonly IAcceptedExchangePlanStore _exchangePlans;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IClock _clock;

        public ServicingResolutionService(
            IServicingReconciliationStore reconciliation,
            IServicingExternalEvidenceStore evidence,
            IServicingManualResolutionStore resolutions,
            IAcceptedExchangePlanStore exchangePlans,
            IUnitOfWork unitOfWork,
            IClock clock)
        {
            _reconciliation = reconciliation;
            _evidence = evidence;
            _resolutions = resolutions;
            _exchangePlans = exchangePlans;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        public async Task<ServicingResolutionOutcome> RecordAsync(
            ServicingResolutionExecution execution,
            CancellationToken cancellationToken = default)
        {
            var request = new ServicingManualResolutionRequest(
                execution.OperationId,
                execution.Kind,
                execution.Actor,
                execution.Reason,
                execution.Reference,
                execution.EvidenceStage,
                execution.ExpectedClaimGeneration);

            var resolutionId = ServicingResolutionPolicy.ResolutionId(request);

            var existing = await _resolutions.FindAsync(
                execution.OperationId, resolutionId, cancellationToken);

            if (existing is not null && existing.ExpectedClaimGeneration == execution.ExpectedClaimGeneration)
                return new ServicingResolutionOutcome(existing, true);

            var operation = await _reconciliation.FindOperationAsync(execution.OperationId, cancellationToken)
                ?? throw ExceptionFactory.ServicingResolutionOperationNotFound(execution.OperationId);

            var evidence = await _evidence.ListAsync(execution.OperationId, cancellationToken);
            var plan = await _exchangePlans.FindAsync(execution.OperationId, cancellationToken);

            var recoveryAction = ServicingRecoveryPolicy.Determine(
                operation, plan, evidence, ServicingRecoveryPolicy.ManualReviewReasons(plan));

            var resolution = ServicingResolutionPolicy.Authorize(
                request, operation, recoveryAction, _clock.GetDateTime());

            var recorded = await _resolutions.AppendAsync(resolution, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ServicingResolutionOutcome(recorded, false);
        }
    }
}
