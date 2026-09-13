using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Servicing.Reconciliation.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingReconciliationStore : IServicingReconciliationStore
    {
        private readonly OrderingDbContext _dbContext;

        public ServicingReconciliationStore(OrderingDbContext dbContext) => _dbContext = dbContext;

        public async Task<ServicingOperationSnapshot?> FindOperationAsync(
            long operationId,
            CancellationToken cancellationToken = default)
            => await Project(
                    _dbContext.Set<ServicingOperation>()
                        .AsNoTracking()
                        .Where(operation => operation.Id == operationId))
                .FirstOrDefaultAsync(cancellationToken);

        public async Task<IReadOnlyList<ServicingOperationSnapshot>> ListUnresolvedAsync(
            long orderId,
            CancellationToken cancellationToken = default)
            => await Project(
                    _dbContext.Set<ServicingOperation>()
                        .AsNoTracking()
                        .Where(operation => operation.OrderId == orderId
                                            && (operation.Status == ServicingOperationStatus.AwaitingExternal
                                                || operation.Status == ServicingOperationStatus.NeedsReconciliation))
                        .OrderBy(operation => operation.Id))
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<ServicingDocumentEvidence>> ListDocumentsAsync(
            long orderId,
            CancellationToken cancellationToken = default)
        {
            var tickets = await _dbContext.Set<ElectronicTicket>()
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

            var documents = await _dbContext.Set<ElectronicMiscDocument>()
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

        public async Task<IReadOnlyList<ServicingControlEvidence>> ListControlAsync(
            long orderId,
            CancellationToken cancellationToken = default)
            => await _dbContext.Set<ElectronicTicket>()
                .AsNoTracking()
                .Where(ticket => ticket.CurrentServicingOrderId == orderId)
                .SelectMany(
                    ticket => ticket.Coupons,
                    (ticket, coupon) => new { ticket, coupon })
                .OrderBy(pair => pair.ticket.Id)
                .ThenBy(pair => pair.coupon.CouponNumber)
                .Select(pair => new ServicingControlEvidence(
                    pair.ticket.Id,
                    pair.ticket.DocumentNumber,
                    pair.coupon.CouponNumber,
                    pair.coupon.ControlStatus,
                    pair.coupon.FinancialStatus))
                .ToListAsync(cancellationToken);

        public async Task<IReadOnlyList<ServicingReservationEvidence>> ListReservationsAsync(
            long orderId,
            CancellationToken cancellationToken = default)
            => await _dbContext.Set<FulfillmentReservation>()
                .AsNoTracking()
                .Where(reservation => reservation.OrderId == orderId)
                .SelectMany(
                    reservation => reservation.Services,
                    (reservation, service) => new { reservation, service })
                .OrderBy(pair => pair.reservation.Id)
                .ThenBy(pair => pair.service.OrderServiceId)
                .Select(pair => new ServicingReservationEvidence(
                    pair.reservation.Id,
                    pair.reservation.OperationId,
                    pair.reservation.ExternalReservationRef,
                    pair.reservation.Status,
                    pair.service.OrderServiceId,
                    pair.service.ExternalServiceRef,
                    pair.service.ObservedStatus,
                    pair.service.ExternalStatus))
                .ToListAsync(cancellationToken);

        private IQueryable<ServicingOperationSnapshot> Project(IQueryable<ServicingOperation> operations)
            => from operation in operations
               join receipt in _dbContext.Set<CommandReceipt>().AsNoTracking()
                   on operation.CommandReceiptId equals receipt.Id into receipts
               from receipt in receipts.DefaultIfEmpty()
               select new ServicingOperationSnapshot(
                   operation.Id,
                   operation.OrderId,
                   operation.Kind,
                   operation.Status,
                   operation.ClaimGeneration,
                   operation.ExpectedCommercialVersion,
                   operation.CreatedAt,
                   operation.UpdatedAt,
                   receipt == null ? null : receipt.CallerScope,
                   receipt == null ? null : receipt.IdempotencyKey,
                   receipt == null ? null : (CommandReceiptStatus?)receipt.Status);
    }
}
