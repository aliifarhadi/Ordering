using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments;
using AeroTech.Framework.Core.ServiceContracts;

namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities
{
    public sealed class EmdCoupon : Entity<long>
    {
        private readonly List<EmdCouponAssociationChange> _associationChanges = new();

        private EmdCoupon()
        {
        }

        internal EmdCoupon(
            long id,
            long electronicMiscDocumentId,
            int couponNumber,
            EmdCouponPurpose purpose,
            string reasonForIssuanceSubCode,
            long? orderServiceId,
            long? pricingLineId,
            string? externalValueReference,
            long? associatedTicketCouponId,
            decimal issuanceValue,
            int currencyId,
            long? predecessorElectronicMiscDocumentId = null,
            string? predecessorDocumentNumber = null,
            int? predecessorCouponNumber = null)
        {
            Id = id;
            ElectronicMiscDocumentId = electronicMiscDocumentId;
            CouponNumber = couponNumber;
            Purpose = purpose;
            ReasonForIssuanceSubCode = reasonForIssuanceSubCode;
            OrderServiceId = orderServiceId;
            PricingLineId = pricingLineId;
            ExternalValueReference = externalValueReference;
            AssociatedTicketCouponId = associatedTicketCouponId;
            IssuanceValue = issuanceValue;
            CurrencyId = currencyId;
            PredecessorElectronicMiscDocumentId = predecessorElectronicMiscDocumentId;
            PredecessorDocumentNumber = predecessorDocumentNumber;
            PredecessorCouponNumber = predecessorCouponNumber;
            Status = EmdCouponStatus.OpenForUse;
        }

        public long ElectronicMiscDocumentId { get; private set; }

        public int CouponNumber { get; private set; }

        public EmdCouponPurpose Purpose { get; private set; }

        public string ReasonForIssuanceSubCode { get; private set; } = default!;

        public long? OrderServiceId { get; private set; }

        public long? PricingLineId { get; private set; }

        public string? ExternalValueReference { get; private set; }

        public long? AssociatedTicketCouponId { get; private set; }

        public decimal IssuanceValue { get; private set; }

        public int CurrencyId { get; private set; }

        public EmdCouponStatus Status { get; private set; }

        public long? PredecessorElectronicMiscDocumentId { get; private set; }

        public string? PredecessorDocumentNumber { get; private set; }

        public int? PredecessorCouponNumber { get; private set; }

        public IReadOnlyList<EmdCouponAssociationChange> AssociationChanges
            => _associationChanges.OrderBy(change => change.Sequence).ToList();

        public bool IsOpenForUse => Status == EmdCouponStatus.OpenForUse;

        public bool IsRefunded => Status == EmdCouponStatus.Refunded;

        public bool IsExchanged => Status == EmdCouponStatus.Exchanged;

        public EmdCouponExchangeRecord? ExchangeRecord { get; private set; }

        public bool IsExchangedBy(long operationId)
            => ExchangeRecord is { } record && record.OperationId == operationId;

        public bool ReplacesAnotherCoupon => PredecessorElectronicMiscDocumentId is not null;

        public EmdCouponRefundRecord? RefundRecord { get; private set; }

        public bool IsRefundedBy(long operationId) => RefundRecord is { } record && record.OperationId == operationId;

        public bool IsAssociatedWith(long ticketCouponId) => AssociatedTicketCouponId == ticketCouponId;

        public bool CarriesNoAssociation => AssociatedTicketCouponId is null;

        public bool IsDisassociatedByReissue(long operationId)
            => CarriesNoAssociation
               && _associationChanges.Any(change =>
                   change.Kind == EmdCouponAssociationChangeKind.DisassociatedByReissue
                   && change.OperationId == operationId);

        public bool IsReassociatedBy(long operationId)
            => _associationChanges.Any(change =>
                change.Kind == EmdCouponAssociationChangeKind.Reassociated
                && change.OperationId == operationId);

        internal void Void() => Status = EmdCouponStatus.Void;

        internal void RecordIssuedAssociation(long operationId, IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (AssociatedTicketCouponId is not { } associated || _associationChanges.Count > 0)
                return;

            Append(
                EmdCouponAssociationChangeKind.Associated,
                null,
                null,
                null,
                associated,
                null,
                null,
                operationId,
                null,
                null,
                idGenerator,
                now);
        }

        internal void Refund(EmdCouponRefund refund, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(refund);

            RefundRecord = new EmdCouponRefundRecord(
                refund.OperationId,
                refund.ApprovedAmount,
                refund.CurrencyId,
                refund.ApprovedDisposition,
                refund.DecisionReference,
                refund.ProviderReference,
                now);

            AssociatedTicketCouponId = null;
            Status = EmdCouponStatus.Refunded;
        }

        internal void Exchange(EmdCouponExchange exchange, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(exchange);

            ExchangeRecord = new EmdCouponExchangeRecord(
                exchange.OperationId,
                exchange.SuccessorElectronicMiscDocumentId,
                exchange.SuccessorDocumentNumber,
                exchange.SuccessorCouponNumber,
                exchange.DecisionReference,
                exchange.ProviderReference,
                now);

            AssociatedTicketCouponId = null;
            Status = EmdCouponStatus.Exchanged;
        }

        internal void DisassociateByReissue(
            EmdCouponDisassociation disassociation,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(disassociation);

            Append(
                EmdCouponAssociationChangeKind.DisassociatedByReissue,
                disassociation.PredecessorTicketCouponId,
                disassociation.PredecessorDocumentNumber,
                disassociation.PredecessorCouponNumber,
                null,
                null,
                null,
                disassociation.OperationId,
                disassociation.DecisionReference,
                disassociation.ProviderReference,
                idGenerator,
                now);

            AssociatedTicketCouponId = null;
        }

        internal void Reassociate(EmdCouponReassociation reassociation, IIdGenerator idGenerator, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(reassociation);

            Append(
                EmdCouponAssociationChangeKind.Reassociated,
                reassociation.PredecessorTicketCouponId,
                reassociation.PredecessorDocumentNumber,
                reassociation.PredecessorCouponNumber,
                reassociation.SuccessorTicketCouponId,
                reassociation.SuccessorDocumentNumber,
                reassociation.SuccessorCouponNumber,
                reassociation.OperationId,
                reassociation.DecisionReference,
                reassociation.ProviderReference,
                idGenerator,
                now);

            AssociatedTicketCouponId = reassociation.SuccessorTicketCouponId;
        }

        private void Append(
            EmdCouponAssociationChangeKind kind,
            long? previousTicketCouponId,
            string? previousDocumentNumber,
            int? previousCouponNumber,
            long? currentTicketCouponId,
            string? currentDocumentNumber,
            int? currentCouponNumber,
            long? operationId,
            string? decisionReference,
            string? providerReference,
            IIdGenerator idGenerator,
            DateTimeOffset now)
            => _associationChanges.Add(new EmdCouponAssociationChange(
                idGenerator.NewId(),
                Id,
                _associationChanges.Count + 1,
                kind,
                previousTicketCouponId,
                previousDocumentNumber,
                previousCouponNumber,
                currentTicketCouponId,
                currentDocumentNumber,
                currentCouponNumber,
                operationId,
                decisionReference,
                providerReference,
                now));
    }
}
