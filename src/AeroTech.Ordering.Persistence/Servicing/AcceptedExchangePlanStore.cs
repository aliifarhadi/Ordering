using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Contracts;
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
            var row = await Query().FirstOrDefaultAsync(plan => plan.OperationId == operationId, cancellationToken);

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
                row.PredecessorTravellerId,
                row.SuccessorElectronicTicketId,
                row.ExpectedCommercialVersion,
                row.MonetaryOutcome,
                accepted,
                row.Coupons
                    .OrderBy(coupon => coupon.PredecessorCouponNumber)
                    .Select(coupon => new AcceptedExchangePlanCoupon(
                        coupon.PredecessorTicketCouponId,
                        coupon.PredecessorCouponNumber,
                        coupon.PredecessorOrderServiceId,
                        coupon.Disposition,
                        coupon.SuccessorTicketCouponId,
                        coupon.ReplacementOrderServiceId,
                        coupon.ReplacementOrderSegmentId,
                        coupon.SuccessorCouponNumber))
                    .ToList(),
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
                PredecessorTravellerId = plan.PredecessorTravellerId,
                SuccessorElectronicTicketId = plan.SuccessorElectronicTicketId,
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
                UpdatedAt = now,
                Coupons = plan.Coupons
                    .Select(coupon => new AcceptedExchangePlanCouponRow
                    {
                        OperationId = plan.OperationId,
                        PredecessorTicketCouponId = coupon.PredecessorTicketCouponId,
                        PredecessorCouponNumber = coupon.PredecessorCouponNumber,
                        PredecessorOrderServiceId = coupon.PredecessorOrderServiceId,
                        Disposition = coupon.Disposition,
                        SuccessorTicketCouponId = coupon.SuccessorTicketCouponId,
                        ReplacementOrderServiceId = coupon.ReplacementOrderServiceId,
                        ReplacementOrderSegmentId = coupon.ReplacementOrderSegmentId,
                        SuccessorCouponNumber = coupon.SuccessorCouponNumber
                    })
                    .ToList()
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
            row.SuccessorIssuerCarrierId = successor.IssuerCarrierId;
            row.SuccessorIssuingOfficeId = successor.IssuingOfficeId;
            row.SuccessorAuthority = successor.Authority;
            row.SuccessorVoidDeadline = successor.VoidDeadline;

            foreach (var identity in successor.Coupons)
            {
                var coupon = row.Coupons.FirstOrDefault(candidate =>
                    candidate.PredecessorCouponNumber == identity.PredecessorCouponNumber);

                if (coupon is not null)
                    coupon.SuccessorCouponNumber = identity.CouponNumber;
            }
        }

        private static SuccessorDocumentIdentity? SuccessorOf(AcceptedExchangePlanRow row)
            => row.SuccessorDocumentNumber is null
               || row.SuccessorIssuerCarrierId is null
               || row.SuccessorAuthority is null
                ? null
                : new SuccessorDocumentIdentity(
                    row.SuccessorDocumentNumber,
                    row.SuccessorIssuerCarrierId.Value,
                    row.SuccessorIssuingOfficeId,
                    row.SuccessorAuthority.Value,
                    row.SuccessorVoidDeadline,
                    row.Coupons
                        .Where(coupon => coupon.SuccessorCouponNumber is not null)
                        .OrderBy(coupon => coupon.PredecessorCouponNumber)
                        .Select(coupon => new SuccessorCouponIdentity(
                            coupon.PredecessorCouponNumber, coupon.SuccessorCouponNumber!.Value))
                        .ToList());

        private IQueryable<AcceptedExchangePlanRow> Query()
            => _dbContext.Set<AcceptedExchangePlanRow>().Include(plan => plan.Coupons);

        private async Task<AcceptedExchangePlanRow> RequireAsync(
            long operationId,
            CancellationToken cancellationToken)
            => await Query().FirstOrDefaultAsync(plan => plan.OperationId == operationId, cancellationToken)
               ?? throw ExceptionFactory.AcceptedExchangePlanNotFound(operationId);
    }
}
