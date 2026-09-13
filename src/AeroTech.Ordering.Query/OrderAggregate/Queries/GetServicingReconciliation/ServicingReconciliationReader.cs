using System.Text.Json;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Policies;
using AeroTech.Ordering.Query._Shared.DbContexts;
using AeroTech.Ordering.Query.OrderAggregate.Models;
using AeroTech.Ordering.Query.OrderAggregate.View;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Query.OrderAggregate.Queries.GetServicingReconciliation
{
    public sealed class ServicingReconciliationReader
    {
        private static readonly JsonSerializerOptions PlanOptions = new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = false
        };

        private readonly OrderQueryDbContext _dbContext;

        public ServicingReconciliationReader(OrderQueryDbContext dbContext) => _dbContext = dbContext;

        public async Task<ServicingReconciliationView?> FindAsync(
            long operationId,
            CancellationToken cancellationToken = default)
        {
            var operation = await _dbContext.ServicingOperations
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == operationId, cancellationToken);

            if (operation is null)
                return null;

            var order = await OrderEvidenceAsync(operation.OrderId, cancellationToken);

            return await ComposeAsync(operation, order, cancellationToken);
        }

        public async Task<IReadOnlyList<ServicingReconciliationView>> ListUnresolvedAsync(
            long orderId,
            CancellationToken cancellationToken = default)
        {
            var operations = await _dbContext.ServicingOperations
                .AsNoTracking()
                .Where(candidate => candidate.OrderId == orderId
                                    && (candidate.Status == ServicingOperationStatus.AwaitingExternal
                                        || candidate.Status == ServicingOperationStatus.NeedsReconciliation))
                .OrderBy(candidate => candidate.Id)
                .ToListAsync(cancellationToken);

            if (operations.Count == 0)
                return [];

            var order = await OrderEvidenceAsync(orderId, cancellationToken);
            var views = new List<ServicingReconciliationView>(operations.Count);

            foreach (var operation in operations)
                views.Add(await ComposeAsync(operation, order, cancellationToken));

            return views;
        }

        private async Task<ServicingReconciliationView> ComposeAsync(
            ServicingOperationReadModel operation,
            OrderEvidence order,
            CancellationToken cancellationToken)
        {
            var receipt = await ReceiptAsync(operation.CommandReceiptId, cancellationToken);
            var evidence = await EvidenceAsync(operation.Id, cancellationToken);
            var resolutions = await ResolutionsAsync(operation.Id, cancellationToken);
            var checkpoints = await CheckpointsAsync(operation.Id, cancellationToken);
            var manualReviews = await ManualReviewReasonsAsync(operation.Id, cancellationToken);

            return new ServicingReconciliationView(
                operation.Id,
                operation.OrderId,
                operation.Kind,
                operation.Status,
                operation.ClaimGeneration,
                operation.ExpectedCommercialVersion,
                operation.CreatedAt,
                operation.UpdatedAt,
                receipt?.CallerScope,
                receipt?.IdempotencyKey,
                receipt?.Status,
                UnresolvedStage(operation.Status, checkpoints, evidence),
                evidence,
                order.Documents,
                order.Control,
                order.Reservations,
                checkpoints,
                manualReviews,
                resolutions,
                ServicingRecoveryPolicy.Determine(
                    operation.Status,
                    evidence.Any(candidate => candidate.IsUnresolved),
                    checkpoints?.IsUnresolved ?? false,
                    manualReviews.Count > 0));
        }

        private static string? UnresolvedStage(
            ServicingOperationStatus status,
            ServicingPlanCheckpoints? checkpoints,
            IReadOnlyList<ServicingExternalEvidence> evidence)
        {
            if (ServicingRecoveryPolicy.IsSettled(status))
                return null;

            return checkpoints is not null
                ? checkpoints.UnresolvedStage
                : evidence.FirstOrDefault(candidate => candidate.IsUnresolved)?.Stage.ToString()
                  ?? evidence.LastOrDefault()?.Stage.ToString();
        }

        private async Task<OrderEvidence> OrderEvidenceAsync(long orderId, CancellationToken cancellationToken)
            => new(
                await DocumentsAsync(orderId, cancellationToken),
                await ControlAsync(orderId, cancellationToken),
                await ReservationsAsync(orderId, cancellationToken));

        private async Task<CommandReceiptReadModel?> ReceiptAsync(
            long? receiptId,
            CancellationToken cancellationToken)
            => receiptId is null
                ? null
                : await _dbContext.CommandReceipts
                    .AsNoTracking()
                    .SingleOrDefaultAsync(receipt => receipt.Id == receiptId, cancellationToken);

        private async Task<IReadOnlyList<ServicingExternalEvidence>> EvidenceAsync(
            long operationId,
            CancellationToken cancellationToken)
            => await _dbContext.ServicingExternalEvidences
                .AsNoTracking()
                .Where(evidence => evidence.OperationId == operationId)
                .OrderBy(evidence => evidence.Stage)
                .Select(evidence => new ServicingExternalEvidence(
                    evidence.OperationId,
                    evidence.Stage,
                    evidence.Outcome,
                    evidence.ProviderReference,
                    evidence.Detail,
                    evidence.DocumentKind,
                    evidence.DocumentNumber,
                    evidence.RecordedAt))
                .ToListAsync(cancellationToken);

        private async Task<IReadOnlyList<ServicingManualResolution>> ResolutionsAsync(
            long operationId,
            CancellationToken cancellationToken)
            => await _dbContext.ServicingManualResolutions
                .AsNoTracking()
                .Where(resolution => resolution.OperationId == operationId)
                .OrderBy(resolution => resolution.RecordedAt)
                .ThenBy(resolution => resolution.ResolutionId)
                .Select(resolution => new ServicingManualResolution(
                    resolution.OperationId,
                    resolution.ResolutionId,
                    resolution.Kind,
                    resolution.Actor,
                    resolution.Reason,
                    resolution.Reference,
                    resolution.EvidenceStage,
                    resolution.ExpectedClaimGeneration,
                    resolution.RecordedAt))
                .ToListAsync(cancellationToken);

        private async Task<IReadOnlyList<ServicingDocumentEvidence>> DocumentsAsync(
            long orderId,
            CancellationToken cancellationToken)
        {
            var tickets = await _dbContext.ElectronicTickets
                .AsNoTracking()
                .Where(ticket => ticket.CurrentServicingOrderId == orderId)
                .OrderBy(ticket => ticket.Id)
                .Select(ticket => new ServicingDocumentEvidence(
                    AccountableDocumentKind.ElectronicTicket,
                    ticket.Id,
                    ticket.DocumentNumber,
                    ticket.StatusSummary.ToString(),
                    ticket.DocumentVersion,
                    ticket.PredecessorElectronicTicketId,
                    ticket.ProviderReference))
                .ToListAsync(cancellationToken);

            var documents = await _dbContext.ElectronicMiscDocuments
                .AsNoTracking()
                .Where(document => document.CurrentServicingOrderId == orderId)
                .OrderBy(document => document.Id)
                .Select(document => new ServicingDocumentEvidence(
                    AccountableDocumentKind.ElectronicMiscDocument,
                    document.Id,
                    document.DocumentNumber,
                    document.StatusSummary.ToString(),
                    document.DocumentVersion,
                    null,
                    document.ProviderReference))
                .ToListAsync(cancellationToken);

            return [.. tickets, .. documents];
        }

        private async Task<IReadOnlyList<ServicingControlEvidence>> ControlAsync(
            long orderId,
            CancellationToken cancellationToken)
            => await (from ticket in _dbContext.ElectronicTickets.AsNoTracking()
                      join coupon in _dbContext.TicketCoupons.AsNoTracking()
                          on ticket.Id equals coupon.TicketId
                      where ticket.CurrentServicingOrderId == orderId
                      orderby ticket.Id, coupon.CouponNumber
                      select new ServicingControlEvidence(
                          ticket.Id,
                          ticket.DocumentNumber,
                          coupon.CouponNumber,
                          coupon.ControlStatus,
                          coupon.FinancialStatus))
                .ToListAsync(cancellationToken);

        private async Task<IReadOnlyList<ServicingReservationEvidence>> ReservationsAsync(
            long orderId,
            CancellationToken cancellationToken)
            => await (from reservation in _dbContext.FulfillmentReservations.AsNoTracking()
                      join service in _dbContext.FulfillmentReservationServices.AsNoTracking()
                          on reservation.Id equals service.FulfillmentReservationId
                      where reservation.OrderId == orderId
                      orderby reservation.Id, service.OrderServiceId
                      select new ServicingReservationEvidence(
                          reservation.Id,
                          reservation.OperationId,
                          reservation.ExternalReservationRef,
                          reservation.Status,
                          service.OrderServiceId,
                          service.ExternalServiceRef,
                          service.ObservedStatus,
                          service.ExternalStatus))
                .ToListAsync(cancellationToken);

        private async Task<IReadOnlyList<string>> ManualReviewReasonsAsync(
            long operationId,
            CancellationToken cancellationToken)
            => await _dbContext.AcceptedExchangePlanAncillaries
                .AsNoTracking()
                .Where(ancillary => ancillary.OperationId == operationId
                                    && ancillary.Disposition == AncillaryExchangeDisposition.ManualReview
                                    && ancillary.ManualReviewReason != null)
                .OrderBy(ancillary => ancillary.EmdCouponId)
                .Select(ancillary => ancillary.ManualReviewReason!)
                .ToListAsync(cancellationToken);

        private async Task<ServicingPlanCheckpoints?> CheckpointsAsync(
            long operationId,
            CancellationToken cancellationToken)
        {
            var plan = await _dbContext.AcceptedExchangePlans
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.OperationId == operationId, cancellationToken);

            if (plan is null)
                return null;

            var accepted = JsonSerializer.Deserialize<AcceptedExchange>(plan.AcceptedPlan, PlanOptions);

            var feeDocuments = await _dbContext.AcceptedExchangePlanFeeDocuments
                .AsNoTracking()
                .Where(document => document.OperationId == operationId)
                .Select(document => document.SettledAt)
                .ToListAsync(cancellationToken);

            var ancillaries = await _dbContext.AcceptedExchangePlanAncillaries
                .AsNoTracking()
                .Where(ancillary => ancillary.OperationId == operationId)
                .Select(ancillary => new ServicingAncillaryCheckpoint(
                    ancillary.Disposition,
                    ancillary.AssociationOutcome,
                    ancillary.RefundDocumentOutcome,
                    ancillary.RefundValueOutcome,
                    ancillary.RetentionSettledAt))
                .ToListAsync(cancellationToken);

            var exchangeGroups = await _dbContext.AcceptedExchangePlanAncillaryExchangeGroups
                .AsNoTracking()
                .Where(group => group.OperationId == operationId)
                .Select(group => new ServicingExchangeGroupCheckpoint(
                    group.ExchangeOutcome,
                    group.SuccessorElectronicMiscDocumentId != null,
                    group.AddCollectAmount != null && group.AddCollectCurrencyId != null,
                    group.FundingCaptureOutcome,
                    group.RefundDueAmount != null && group.RefundDueCurrencyId != null,
                    group.RefundDueOutcome,
                    group.ResidualAmount != null
                        && group.ResidualCurrencyId != null
                        && group.ResidualFulfillment != ResidualFulfillment.DocumentCoupled,
                    group.ResidualOutcome))
                .ToListAsync(cancellationToken);

            var cancelGroups = await _dbContext.AcceptedExchangePlanAncillaryCancelGroups
                .AsNoTracking()
                .Where(group => group.OperationId == operationId)
                .Select(group => group.CancellationSettledAt)
                .ToListAsync(cancellationToken);

            return ServicingPlanCheckpoints.From(
                plan.EligibilityOutcome == DocumentExchangeEligibilityOutcome.Eligible,
                plan.ReservationOutcome,
                plan.DocumentExchangeOutcome,
                new ServicingMonetaryCheckpoint(
                    accepted?.AddCollect is not null,
                    plan.FundingCaptureOutcome,
                    accepted?.RefundDue is not null,
                    plan.RefundDueOutcome,
                    accepted?.Residual is not null,
                    plan.ResidualOutcome),
                feeDocuments,
                ancillaries,
                exchangeGroups,
                cancelGroups);
        }

        private sealed record OrderEvidence(
            IReadOnlyList<ServicingDocumentEvidence> Documents,
            IReadOnlyList<ServicingControlEvidence> Control,
            IReadOnlyList<ServicingReservationEvidence> Reservations);
    }
}
