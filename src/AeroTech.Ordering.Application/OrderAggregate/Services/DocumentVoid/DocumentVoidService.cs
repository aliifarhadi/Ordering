using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.Ports.DocumentVoid;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.DocumentVoid
{
    public sealed class DocumentVoidService : IDocumentVoidService
    {
        public const string VoidStep = "document-void";

        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IElectronicMiscDocumentRepository _miscDocuments;
        private readonly IDocumentVoidPort _provider;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public DocumentVoidService(
            IOrderRepository orders,
            IElectronicTicketRepository tickets,
            IElectronicMiscDocumentRepository miscDocuments,
            IDocumentVoidPort provider,
            IOrderOperationCoordinator operations,
            IServicingOperationStore operationStore,
            ICommandReceiptStore receipts,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector)
        {
            _orders = orders;
            _tickets = tickets;
            _miscDocuments = miscDocuments;
            _provider = provider;
            _operations = operations;
            _operationStore = operationStore;
            _receipts = receipts;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<DocumentVoidOutcome> VoidAsync(
            long orderId,
            long documentId,
            VoidReason reason,
            string? reasonDetail,
            long voidedBy,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var target = await ResolveAsync(orderId, documentId, cancellationToken);

            var operation = await _operations.BeginAsync(
                orderId,
                ServicingOperationKind.VoidDocument,
                idempotencyKey,
                new
                {
                    Operation = "VoidDocument",
                    OrderId = orderId,
                    DocumentKind = target.Kind.ToString(),
                    DocumentId = documentId,
                    Reason = reason.ToString(),
                    ReasonDetail = reasonDetail
                },
                null,
                cancellationToken);

            if (target.IsVoided)
                return await ReplayFinalizedAsync(order, operation, target, cancellationToken);

            var provenance = new VoidProvenance(reason, reasonDetail, voidedBy);

            if (operation.IsReplay)
            {
                var unfinished = await ReplayUnfinishedAsync(order, operation, target, provenance, cancellationToken);

                if (unfinished is not null)
                    return unfinished;
            }

            ProviderOperationOutcome outcome;
            string? providerReference;

            try
            {
                target.EnsureCanBeVoided(_clock.GetDateTime());

                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.Executing,
                    operation.ClaimGeneration,
                    cancellationToken);

                var eligibility = await _provider.CheckEligibilityAsync(
                    new DocumentVoidEligibilityRequest(
                        VoidKey(operation, target),
                        orderId,
                        operation.OperationId,
                        target.Kind,
                        target.DocumentNumber),
                    cancellationToken);

                if (eligibility.Outcome == EligibilityOutcome.Denied)
                    return await RefuseAsync(order, operation, target, eligibility, cancellationToken);

                if (eligibility.Outcome == EligibilityOutcome.PendingEvidence)
                    return await SuspendAsync(order, operation, target, ProviderOperationOutcome.Pending, cancellationToken);

                var result = await _provider.VoidAsync(
                    new DocumentVoidRequest(
                        VoidKey(operation, target),
                        orderId,
                        operation.OperationId,
                        target.Kind,
                        target.DocumentNumber,
                        target.IssuerCarrierId),
                    cancellationToken);

                outcome = result.Outcome;
                providerReference = result.ProviderReference;
            }
            catch
            {
                await TryReleaseRejectedAsync(orderId, operation, cancellationToken);
                throw;
            }

            return outcome switch
            {
                ProviderOperationOutcome.Confirmed =>
                    await FinalizeAsync(order, operation, target, provenance, providerReference, cancellationToken),
                ProviderOperationOutcome.Rejected => await RejectAsync(order, operation, target, cancellationToken),
                _ => await SuspendAsync(order, operation, target, outcome, cancellationToken)
            };
        }

        private async Task<DocumentVoidOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            VoidTarget target,
            VoidProvenance provenance,
            string? providerReference,
            CancellationToken cancellationToken)
        {
            target.Void(operation.OperationId, provenance, providerReference, _idGenerator, _clock);
            order.ApplyDocumentVoid(target.AffectedServiceIds, _clock);

            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Completed,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Completed, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            await _projector.ProjectAsync(order.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, target, ProviderOperationOutcome.Confirmed, ServicingOperationStatus.Completed, false, false);
        }

        private async Task<DocumentVoidOutcome> RejectAsync(
            Order order,
            OrderOperation operation,
            VoidTarget target,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Rejected,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Rejected, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, target, ProviderOperationOutcome.Rejected, ServicingOperationStatus.Rejected, false, false);
        }

        private async Task<DocumentVoidOutcome> RefuseAsync(
            Order order,
            OrderOperation operation,
            VoidTarget target,
            DocumentVoidEligibility eligibility,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Rejected,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Rejected, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(
                order,
                operation,
                target,
                ProviderOperationOutcome.Rejected,
                ServicingOperationStatus.Rejected,
                eligibility.RefundRequiredInstead,
                false);
        }

        private async Task<DocumentVoidOutcome> SuspendAsync(
            Order order,
            OrderOperation operation,
            VoidTarget target,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.AwaitingExternal,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(
                operation.ReceiptId,
                outcome == ProviderOperationOutcome.Unknown
                    ? CommandReceiptStatus.Unknown
                    : CommandReceiptStatus.Pending,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, target, outcome, ServicingOperationStatus.AwaitingExternal, false, false);
        }

        private async Task<DocumentVoidOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            VoidTarget target,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.NeedsReconciliation,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.NeedsReconciliation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, target, outcome, ServicingOperationStatus.NeedsReconciliation, false, true);
        }

        private async Task<DocumentVoidOutcome?> ReplayUnfinishedAsync(
            Order order,
            OrderOperation operation,
            VoidTarget target,
            VoidProvenance provenance,
            CancellationToken cancellationToken)
        {
            var prior = await _operationStore.FindAsync(operation.OperationId, cancellationToken);

            if (prior is null)
                return null;

            if (prior.Status == ServicingOperationStatus.Rejected)
                return Outcome(order, operation, target, ProviderOperationOutcome.Rejected, prior.Status, false, true);

            if (prior.Status is not (ServicingOperationStatus.Executing
                or ServicingOperationStatus.AwaitingExternal
                or ServicingOperationStatus.NeedsReconciliation))
                return null;

            var recovery = await _provider.RecoverAsync(
                new DocumentVoidRecoveryRequest(
                    VoidKey(operation, target),
                    order.Id,
                    operation.OperationId,
                    target.Kind,
                    target.DocumentNumber),
                cancellationToken);

            return recovery.Outcome switch
            {
                ProviderOperationOutcome.Confirmed =>
                    await FinalizeAsync(order, operation, target, provenance, recovery.ProviderReference, cancellationToken),
                ProviderOperationOutcome.Rejected => await RejectAsync(order, operation, target, cancellationToken),
                _ => await ReconcileAsync(order, operation, target, recovery.Outcome, cancellationToken)
            };
        }

        private async Task<DocumentVoidOutcome> ReplayFinalizedAsync(
            Order order,
            OrderOperation operation,
            VoidTarget target,
            CancellationToken cancellationToken)
        {
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, target, ProviderOperationOutcome.Confirmed, ServicingOperationStatus.Completed, false, true);
        }

        private async Task<VoidTarget> ResolveAsync(long orderId, long documentId, CancellationToken cancellationToken)
        {
            var tickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);
            var ticket = tickets.FirstOrDefault(candidate => candidate.Id == documentId);

            if (ticket is not null)
                return VoidTarget.For(ticket);

            var documents = await _miscDocuments.ListByOrderAsync(orderId, cancellationToken);
            var document = documents.FirstOrDefault(candidate => candidate.Id == documentId);

            return document is not null
                ? VoidTarget.For(document)
                : throw ExceptionFactory.AccountableDocumentNotFound(documentId, orderId);
        }

        private string VoidKey(OrderOperation operation, VoidTarget target)
            => _operations.ProviderOperationKey(operation, $"{VoidStep}:{target.DocumentId}");

        private async Task TryReleaseRejectedAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken)
        {
            try
            {
                await _operations.ResolveAsync(orderId, operation, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
            }
        }

        private static DocumentVoidOutcome Outcome(
            Order order,
            OrderOperation operation,
            VoidTarget target,
            ProviderOperationOutcome providerOutcome,
            ServicingOperationStatus operationStatus,
            bool refundRequiredInstead,
            bool isReplay)
            => new(
                order.Id,
                operation.OperationId,
                target.Kind,
                target.DocumentId,
                target.DocumentNumber,
                target.DocumentVersion,
                target.AffectedServiceIds,
                providerOutcome,
                operationStatus,
                refundRequiredInstead,
                isReplay);

        private sealed record VoidProvenance(VoidReason Reason, string? ReasonDetail, long VoidedBy);

        private sealed class VoidTarget
        {
            private readonly ElectronicTicket? _ticket;
            private readonly ElectronicMiscDocument? _document;

            private VoidTarget(ElectronicTicket? ticket, ElectronicMiscDocument? document)
            {
                _ticket = ticket;
                _document = document;
            }

            public static VoidTarget For(ElectronicTicket ticket) => new(ticket, null);

            public static VoidTarget For(ElectronicMiscDocument document) => new(null, document);

            public AccountableDocumentKind Kind => _ticket is not null
                ? AccountableDocumentKind.ElectronicTicket
                : AccountableDocumentKind.ElectronicMiscDocument;

            public long DocumentId => _ticket?.Id ?? _document!.Id;

            public string DocumentNumber => _ticket?.DocumentNumber ?? _document!.DocumentNumber;

            public long IssuerCarrierId => _ticket?.IssuerCarrierId ?? _document!.IssuerCarrierId;

            public int DocumentVersion => _ticket?.DocumentVersion ?? _document!.DocumentVersion;

            public bool IsVoided => _ticket is not null
                ? _ticket.StatusSummary == ElectronicTicketStatus.Voided
                : _document!.StatusSummary == ElectronicMiscDocumentStatus.Voided;

            public IReadOnlyList<long> AffectedServiceIds => _ticket is not null
                ? _ticket.VoidedServiceIds().ToList()
                : _document!.VoidedServiceIds().ToList();

            public void EnsureCanBeVoided(DateTimeOffset now)
            {
                if (_ticket is not null)
                    _ticket.EnsureCanBeVoided(now);
                else
                    _document!.EnsureCanBeVoided();
            }

            public void Void(
                long operationId,
                VoidProvenance provenance,
                string? providerReference,
                IIdGenerator idGenerator,
                IClock clock)
            {
                if (_ticket is not null)
                    _ticket.Void(
                        operationId,
                        provenance.Reason,
                        provenance.ReasonDetail,
                        provenance.VoidedBy,
                        providerReference,
                        idGenerator,
                        clock);
                else
                    _document!.Void(
                        operationId,
                        provenance.Reason,
                        provenance.ReasonDetail,
                        provenance.VoidedBy,
                        providerReference,
                        idGenerator,
                        clock);
            }
        }
    }
}
