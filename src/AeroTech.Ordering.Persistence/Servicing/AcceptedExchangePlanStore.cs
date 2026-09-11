using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Contracts;
using AeroTech.Ordering.Domain._Shared.Documents;
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
                        new TicketedSegmentSnapshot(
                            coupon.SegmentMarketingAirlineId,
                            coupon.SegmentFlightNumber,
                            coupon.SegmentOriginAirportId,
                            coupon.SegmentDestinationAirportId,
                            coupon.SegmentDepartureDateTime,
                            coupon.SegmentArrivalDateTime,
                            coupon.SegmentBookingClass),
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
                SuccessorOf(row),
                row.FundingMethodRef,
                row.FundingGuaranteeOutcome,
                row.FundingGuaranteeReference,
                row.FundingGuaranteeDetail,
                row.FundingCaptureOutcome,
                row.FundingCaptureReference,
                row.FundingCaptureDetail,
                row.FundingReleaseOutcome,
                row.FundingReleaseDetail,
                row.RefundDueOutcome,
                row.RefundDueReference,
                row.RefundDueDetail,
                row.ResidualOutcome,
                row.ResidualProviderReference,
                row.ResidualInstrumentReference,
                row.ResidualInstrument,
                row.ResidualDetail,
                row.Ancillaries
                    .OrderBy(ancillary => ancillary.EmdDocumentNumber)
                    .ThenBy(ancillary => ancillary.EmdCouponNumber)
                    .Select(ancillary => new AcceptedExchangeAncillaryDisposition(
                        ancillary.ElectronicMiscDocumentId,
                        ancillary.EmdDocumentNumber,
                        ancillary.EmdCouponNumber,
                        ancillary.EmdCouponId,
                        ancillary.PredecessorTicketCouponId,
                        ancillary.PredecessorDocumentNumber,
                        ancillary.PredecessorCouponNumber,
                        ancillary.Disposition,
                        ancillary.TargetPredecessorCouponNumber,
                        ancillary.TargetSuccessorTicketCouponId,
                        ancillary.DecisionReference,
                        ancillary.DecisionVersion,
                        ancillary.DecisionContextFingerprint,
                        ancillary.AssociationOutcome,
                        ancillary.AssociationProviderReference,
                        ancillary.AssociationDetail))
                    .ToList());
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
                DocumentExchangeSuccessorEvidence = null,
                FundingMethodRef = plan.FundingMethodRef,
                FundingGuaranteeOutcome = plan.FundingGuaranteeOutcome,
                FundingGuaranteeReference = plan.FundingGuaranteeReference,
                FundingGuaranteeDetail = plan.FundingGuaranteeDetail,
                FundingCaptureOutcome = plan.FundingCaptureOutcome,
                FundingCaptureReference = plan.FundingCaptureReference,
                FundingCaptureDetail = plan.FundingCaptureDetail,
                FundingReleaseOutcome = plan.FundingReleaseOutcome,
                FundingReleaseDetail = plan.FundingReleaseDetail,
                RefundDueOutcome = plan.RefundDueOutcome,
                RefundDueReference = plan.RefundDueReference,
                RefundDueDetail = plan.RefundDueDetail,
                ResidualOutcome = plan.ResidualOutcome,
                ResidualProviderReference = plan.ResidualProviderReference,
                ResidualInstrumentReference = plan.ResidualInstrumentReference,
                ResidualInstrument = plan.ResidualInstrument,
                ResidualDetail = plan.ResidualDetail,
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
                        SuccessorCouponNumber = coupon.SuccessorCouponNumber,
                        SegmentMarketingAirlineId = coupon.TicketedSegment.MarketingAirlineId,
                        SegmentFlightNumber = coupon.TicketedSegment.FlightNumber,
                        SegmentOriginAirportId = coupon.TicketedSegment.OriginAirportId,
                        SegmentDestinationAirportId = coupon.TicketedSegment.DestinationAirportId,
                        SegmentDepartureDateTime = coupon.TicketedSegment.DepartureDateTime,
                        SegmentArrivalDateTime = coupon.TicketedSegment.ArrivalDateTime,
                        SegmentBookingClass = coupon.TicketedSegment.BookingClass
                    })
                    .ToList(),
                Ancillaries = plan.Ancillaries
                    .Select(ancillary => new AcceptedExchangePlanAncillaryRow
                    {
                        OperationId = plan.OperationId,
                        ElectronicMiscDocumentId = ancillary.ElectronicMiscDocumentId,
                        EmdDocumentNumber = ancillary.EmdDocumentNumber,
                        EmdCouponNumber = ancillary.EmdCouponNumber,
                        EmdCouponId = ancillary.EmdCouponId,
                        PredecessorTicketCouponId = ancillary.PredecessorTicketCouponId,
                        PredecessorDocumentNumber = ancillary.PredecessorDocumentNumber,
                        PredecessorCouponNumber = ancillary.PredecessorCouponNumber,
                        Disposition = ancillary.Disposition,
                        TargetPredecessorCouponNumber = ancillary.TargetPredecessorCouponNumber,
                        TargetSuccessorTicketCouponId = ancillary.TargetSuccessorTicketCouponId,
                        DecisionReference = ancillary.DecisionReference,
                        DecisionVersion = ancillary.DecisionVersion,
                        DecisionContextFingerprint = ancillary.DecisionContextFingerprint,
                        AssociationOutcome = ancillary.AssociationOutcome,
                        AssociationProviderReference = ancillary.AssociationProviderReference,
                        AssociationDetail = ancillary.AssociationDetail
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

        public async Task RecordFundingGuaranteeOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.FundingGuaranteeOutcome = outcome;
            row.FundingGuaranteeReference = providerReference ?? row.FundingGuaranteeReference;
            row.FundingGuaranteeDetail = detail ?? row.FundingGuaranteeDetail;
            row.UpdatedAt = _clock.GetDateTime();
        }

        public async Task RecordFundingCaptureOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.FundingCaptureOutcome = outcome;
            row.FundingCaptureReference = providerReference ?? row.FundingCaptureReference;
            row.FundingCaptureDetail = detail ?? row.FundingCaptureDetail;
            row.UpdatedAt = _clock.GetDateTime();
        }

        public async Task RecordFundingReleaseOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.FundingReleaseOutcome = outcome;
            row.FundingReleaseDetail = detail ?? row.FundingReleaseDetail;
            row.UpdatedAt = _clock.GetDateTime();
        }

        public async Task RecordRefundDueOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? valueMovementReference,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.RefundDueOutcome = outcome;
            row.RefundDueReference = valueMovementReference ?? row.RefundDueReference;
            row.RefundDueDetail = detail ?? row.RefundDueDetail;
            row.UpdatedAt = _clock.GetDateTime();
        }

        public async Task RecordResidualOutcomeAsync(
            long operationId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? instrumentReference,
            ResidualInstrumentKind? instrument,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await RequireAsync(operationId, cancellationToken);

            row.ResidualOutcome = outcome;
            row.ResidualProviderReference = providerReference ?? row.ResidualProviderReference;
            row.ResidualInstrumentReference = instrumentReference ?? row.ResidualInstrumentReference;
            row.ResidualInstrument = instrument ?? row.ResidualInstrument;
            row.ResidualDetail = detail ?? row.ResidualDetail;
            row.UpdatedAt = _clock.GetDateTime();
        }

        public async Task RecordAncillaryAssociationOutcomeAsync(
            long operationId,
            long emdCouponId,
            ProviderOperationOutcome outcome,
            string? providerReference,
            string? detail,
            CancellationToken cancellationToken = default)
        {
            var row = await _dbContext.Set<AcceptedExchangePlanAncillaryRow>()
                          .FirstOrDefaultAsync(
                              ancillary => ancillary.OperationId == operationId
                                           && ancillary.EmdCouponId == emdCouponId,
                              cancellationToken)
                      ?? throw ExceptionFactory.AcceptedExchangePlanNotFound(operationId);

            row.AssociationOutcome = outcome;
            row.AssociationProviderReference = providerReference ?? row.AssociationProviderReference;
            row.AssociationDetail = detail ?? row.AssociationDetail;

            var plan = await RequireAsync(operationId, cancellationToken);

            plan.UpdatedAt = _clock.GetDateTime();
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

            row.DocumentExchangeSuccessorEvidence = JsonSerializer.Serialize(successor, PlanOptions);

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
            => row.DocumentExchangeSuccessorEvidence is { } evidence
                ? JsonSerializer.Deserialize<SuccessorDocumentIdentity>(evidence, PlanOptions)
                : NormalizedSuccessorOf(row);

        private static SuccessorDocumentIdentity? NormalizedSuccessorOf(AcceptedExchangePlanRow row)
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
            => _dbContext.Set<AcceptedExchangePlanRow>()
                .Include(plan => plan.Coupons)
                .Include(plan => plan.Ancillaries);

        private async Task<AcceptedExchangePlanRow> RequireAsync(
            long operationId,
            CancellationToken cancellationToken)
            => await Query().FirstOrDefaultAsync(plan => plan.OperationId == operationId, cancellationToken)
               ?? throw ExceptionFactory.AcceptedExchangePlanNotFound(operationId);
    }
}
