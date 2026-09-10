using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Contracts;
using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain._Shared.Resources;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanStore : IAcceptedExchangePlanStore
    {
        private static readonly JsonSerializerOptions PlanOptions = new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        private readonly OrderingDbContext _dbContext;
        private readonly IClock _clock;

        public AcceptedExchangePlanStore(OrderingDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext;
            _clock = clock;
        }

        public async Task<AcceptedExchangePlan?> FindAsync(
            long operationId,
            CancellationToken cancellationToken = default)
        {
            var row = await _dbContext.Set<AcceptedExchangePlanRow>()
                .FirstOrDefaultAsync(plan => plan.OperationId == operationId, cancellationToken);

            if (row is null)
                return null;

            var accepted = JsonSerializer.Deserialize<AcceptedExchange>(row.AcceptedPlan, PlanOptions)
                           ?? throw ExceptionFactory.AcceptedExchangePlanNotFound(operationId);

            return new AcceptedExchangePlan(
                row.OperationId,
                row.OrderId,
                row.QuotedExchangeId,
                row.SourceSystem,
                row.TargetSelectionRef,
                row.SourcePricingReference,
                row.PricingSource,
                row.SaleCurrencyId,
                row.PredecessorElectronicTicketId,
                row.PredecessorDocumentNumber,
                row.PredecessorTicketCouponId,
                row.PredecessorCouponNumber,
                row.PredecessorOrderServiceId,
                row.ReplacementOrderServiceId,
                row.ReplacementOrderSegmentId,
                row.SuccessorElectronicTicketId,
                row.SuccessorTicketCouponId,
                row.ExpectedCommercialVersion,
                row.MonetaryOutcome,
                accepted,
                row.Disposition,
                row.DispositionDetail,
                row.RejectionCode,
                row.RejectionHttpStatus,
                row.EligibilityOutcome,
                row.EligibilityDetail,
                row.ReservationOutcome,
                row.ReservationExternalRef,
                row.DocumentExchangeOutcome,
                row.DocumentExchangeProviderReference,
                row.DocumentExchangeDetail,
                SuccessorOf(row));
        }

        public async Task SaveAsync(AcceptedExchangePlan plan, CancellationToken cancellationToken = default)
        {
            var existing = await _dbContext.Set<AcceptedExchangePlanRow>()
                .FirstOrDefaultAsync(row => row.OperationId == plan.OperationId, cancellationToken);

            if (existing is not null)
                return;

            var now = _clock.GetDateTime();

            var row = new AcceptedExchangePlanRow
            {
                OperationId = plan.OperationId,
                OrderId = plan.OrderId,
                QuotedExchangeId = plan.QuotedExchangeId,
                SourceSystem = plan.SourceSystem,
                TargetSelectionRef = plan.TargetSelectionRef,
                SourcePricingReference = plan.SourcePricingReference,
                PricingSource = plan.PricingSource,
                SaleCurrencyId = plan.SaleCurrencyId,
                PredecessorElectronicTicketId = plan.PredecessorElectronicTicketId,
                PredecessorDocumentNumber = plan.PredecessorDocumentNumber,
                PredecessorTicketCouponId = plan.PredecessorTicketCouponId,
                PredecessorCouponNumber = plan.PredecessorCouponNumber,
                PredecessorOrderServiceId = plan.PredecessorOrderServiceId,
                ReplacementOrderServiceId = plan.ReplacementOrderServiceId,
                ReplacementOrderSegmentId = plan.ReplacementOrderSegmentId,
                SuccessorElectronicTicketId = plan.SuccessorElectronicTicketId,
                SuccessorTicketCouponId = plan.SuccessorTicketCouponId,
                ExpectedCommercialVersion = plan.ExpectedCommercialVersion,
                MonetaryOutcome = plan.MonetaryOutcome,
                AcceptedPlan = JsonSerializer.Serialize(plan.Accepted, PlanOptions),
                Disposition = plan.Disposition,
                DispositionDetail = plan.DispositionDetail,
                RejectionCode = plan.RejectionCode,
                RejectionHttpStatus = plan.RejectionHttpStatus,
                EligibilityOutcome = plan.EligibilityOutcome,
                EligibilityDetail = plan.EligibilityDetail,
                ReservationOutcome = plan.ReservationOutcome,
                ReservationExternalRef = plan.ReservationExternalRef,
                DocumentExchangeOutcome = plan.DocumentExchangeOutcome,
                DocumentExchangeProviderReference = plan.DocumentExchangeProviderReference,
                DocumentExchangeDetail = plan.DocumentExchangeDetail,
                CreatedAt = now,
                UpdatedAt = now
            };

            ApplySuccessor(row, plan.Successor);

            await _dbContext.Set<AcceptedExchangePlanRow>().AddAsync(row, cancellationToken);
        }

        public async Task RecordEligibilityOutcomeAsync(
            long operationId,
            DocumentExchangeEligibilityOutcome outcome,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.EligibilityOutcome = outcome;
            row.EligibilityDetail = detail ?? row.EligibilityDetail;
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

        public async Task RecordDocumentExchangeOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            SuccessorDocumentIdentity? successor,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.DocumentExchangeOutcome = outcome;
            row.DocumentExchangeProviderReference = providerReference ?? row.DocumentExchangeProviderReference;
            row.DocumentExchangeDetail = detail ?? row.DocumentExchangeDetail;
            ApplySuccessor(row, successor);
            row.UpdatedAt = _clock.GetDateTime();
        }

        private static void ApplySuccessor(AcceptedExchangePlanRow row, SuccessorDocumentIdentity? successor)
        {
            if (successor is null)
                return;

            row.SuccessorDocumentNumber = successor.DocumentNumber;
            row.SuccessorCouponNumber = successor.CouponNumber;
            row.SuccessorIssuerCarrierId = successor.IssuerCarrierId;
            row.SuccessorIssuingOfficeId = successor.IssuingOfficeId;
            row.SuccessorAuthority = successor.Authority;
            row.SuccessorVoidDeadline = successor.VoidDeadline;
        }

        private static SuccessorDocumentIdentity? SuccessorOf(AcceptedExchangePlanRow row)
            => row.SuccessorDocumentNumber is null
               || row.SuccessorCouponNumber is null
               || row.SuccessorIssuerCarrierId is null
               || row.SuccessorAuthority is null
                ? null
                : new SuccessorDocumentIdentity(
                    row.SuccessorDocumentNumber,
                    row.SuccessorCouponNumber.Value,
                    row.SuccessorIssuerCarrierId.Value,
                    row.SuccessorIssuingOfficeId,
                    row.SuccessorAuthority.Value,
                    row.SuccessorVoidDeadline);

        private async Task<AcceptedExchangePlanRow> RequireAsync(
            long operationId,
            CancellationToken cancellationToken)
            => await _dbContext.Set<AcceptedExchangePlanRow>()
                   .FirstOrDefaultAsync(plan => plan.OperationId == operationId, cancellationToken)
               ?? throw ExceptionFactory.AcceptedExchangePlanNotFound(operationId);
    }
}
