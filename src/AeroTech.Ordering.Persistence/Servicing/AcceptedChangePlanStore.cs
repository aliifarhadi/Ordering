using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.VoluntaryChange;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class AcceptedChangePlanStore : IAcceptedChangePlanStore
    {
        private static readonly JsonSerializerOptions PlanOptions = new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        private readonly OrderingDbContext _dbContext;
        private readonly IClock _clock;

        public AcceptedChangePlanStore(OrderingDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public async Task<AcceptedChangePlan?> FindAsync(
            long operationId,
            CancellationToken cancellationToken = default)
        {
            var row = await _dbContext.Set<AcceptedChangePlanRow>()
                .FirstOrDefaultAsync(plan => plan.OperationId == operationId, cancellationToken);

            if (row is null)
                return null;

            var accepted = JsonSerializer.Deserialize<AcceptedVoluntaryChange>(row.AcceptedPlan, PlanOptions)
                           ?? throw ExceptionFactory.AcceptedChangePlanNotFound(operationId);

            return new AcceptedChangePlan(
                row.OperationId,
                row.OrderId,
                row.QuotedChangeId,
                row.SourceSystem,
                row.TargetSelectionRef,
                row.ElectronicTicketId,
                row.ReplacedOrderServiceId,
                row.ReplacementOrderServiceId,
                row.ReplacementOrderSegmentId,
                row.TicketCouponId,
                row.ExpectedCommercialVersion,
                row.MonetaryOutcome,
                accepted,
                row.ReservationOutcome,
                row.ReservationExternalRef,
                row.EligibilityOutcome,
                row.EligibilityDetail,
                row.RevalidationOutcome,
                row.RevalidationProviderReference,
                row.RevalidationDetail);
        }

        public async Task SaveAsync(AcceptedChangePlan plan, CancellationToken cancellationToken = default)
        {
            var existing = await _dbContext.Set<AcceptedChangePlanRow>()
                .FirstOrDefaultAsync(row => row.OperationId == plan.OperationId, cancellationToken);

            if (existing is not null)
                return;

            var now = _clock.GetDateTime();

            await _dbContext.Set<AcceptedChangePlanRow>().AddAsync(
                new AcceptedChangePlanRow
                {
                    OperationId = plan.OperationId,
                    OrderId = plan.OrderId,
                    QuotedChangeId = plan.QuotedChangeId,
                    SourceSystem = plan.SourceSystem,
                    TargetSelectionRef = plan.TargetSelectionRef,
                    ElectronicTicketId = plan.ElectronicTicketId,
                    ReplacedOrderServiceId = plan.ReplacedOrderServiceId,
                    ReplacementOrderServiceId = plan.ReplacementOrderServiceId,
                    ReplacementOrderSegmentId = plan.ReplacementOrderSegmentId,
                    TicketCouponId = plan.TicketCouponId,
                    ExpectedCommercialVersion = plan.ExpectedCommercialVersion,
                    MonetaryOutcome = plan.MonetaryOutcome,
                    AcceptedPlan = JsonSerializer.Serialize(plan.Accepted, PlanOptions),
                    ReservationOutcome = plan.ReservationOutcome,
                    ReservationExternalRef = plan.ReservationExternalRef,
                    EligibilityOutcome = plan.EligibilityOutcome,
                    EligibilityDetail = plan.EligibilityDetail,
                    RevalidationOutcome = plan.RevalidationOutcome,
                    RevalidationProviderReference = plan.RevalidationProviderReference,
                    RevalidationDetail = plan.RevalidationDetail,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                cancellationToken);
        }

        public async Task RecordEligibilityOutcomeAsync(
            long operationId,
            DocumentChangeEligibilityOutcome outcome,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.EligibilityOutcome = outcome;
            row.EligibilityDetail = detail ?? row.EligibilityDetail;
            row.UpdatedAt = _clock.GetDateTime();
        }

        public async Task RecordRevalidationOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.RevalidationOutcome = outcome;
            row.RevalidationProviderReference = providerReference ?? row.RevalidationProviderReference;
            row.RevalidationDetail = detail ?? row.RevalidationDetail;
            row.UpdatedAt = _clock.GetDateTime();
        }

        public async Task RecordReservationOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? externalReservationRef,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.ReservationOutcome = outcome;
            row.ReservationExternalRef = externalReservationRef ?? row.ReservationExternalRef;
            row.UpdatedAt = _clock.GetDateTime();
        }

        private async Task<AcceptedChangePlanRow> RequireAsync(
            long operationId,
            CancellationToken cancellationToken)
            => await _dbContext.Set<AcceptedChangePlanRow>()
                   .FirstOrDefaultAsync(plan => plan.OperationId == operationId, cancellationToken)
               ?? throw ExceptionFactory.AcceptedChangePlanNotFound(operationId);
    }
}
