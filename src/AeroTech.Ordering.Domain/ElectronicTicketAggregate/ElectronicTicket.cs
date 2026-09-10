using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate
{
    public sealed class ElectronicTicket : AggregateRoot<long>
    {
        private readonly List<TicketCoupon> _coupons = new();
        private readonly List<DocumentPriceLink> _priceLinks = new();
        private readonly List<DocumentRefundRecord> _refunds = new();
        private readonly List<DocumentRefundCorrectionRecord> _refundCorrections = new();
        private readonly List<DocumentRevalidationRecord> _revalidations = new();
        private readonly List<DocumentExchangeRecord> _exchanges = new();

        private ElectronicTicket()
        {
        }

        private ElectronicTicket(
            long id,
            long originalOrderId,
            long travelerId,
            long operationId,
            string documentNumber,
            long issuerCarrierId,
            long? issuingOfficeId,
            DocumentAuthority authority,
            DateTimeOffset issuedAt,
            DateTimeOffset? voidDeadline,
            decimal issuedTotal,
            int currencyId)
        {
            Id = id;
            OriginalOrderId = originalOrderId;
            CurrentServicingOrderId = originalOrderId;
            TravelerId = travelerId;
            OperationId = operationId;
            DocumentNumber = documentNumber;
            IssuerCarrierId = issuerCarrierId;
            IssuingOfficeId = issuingOfficeId;
            Authority = authority;
            IssuedAt = issuedAt;
            VoidDeadline = voidDeadline;
            IssuedTotal = issuedTotal;
            CurrencyId = currencyId;
            StatusSummary = ElectronicTicketStatus.Issued;
            DocumentVersion = 1;
        }

        public long OriginalOrderId { get; private set; }

        public long CurrentServicingOrderId { get; private set; }

        public long TravelerId { get; private set; }

        public long OperationId { get; private set; }

        public string DocumentNumber { get; private set; } = default!;

        public long IssuerCarrierId { get; private set; }

        public long? IssuingOfficeId { get; private set; }

        public DocumentAuthority Authority { get; private set; }

        public DateTimeOffset IssuedAt { get; private set; }

        public DateTimeOffset? VoidDeadline { get; private set; }

        public decimal IssuedTotal { get; private set; }

        public int CurrencyId { get; private set; }

        public ElectronicTicketStatus StatusSummary { get; private set; }

        public int DocumentVersion { get; private set; }

        public IReadOnlyCollection<TicketCoupon> Coupons => _coupons.AsReadOnly();

        public IReadOnlyCollection<DocumentPriceLink> PriceLinks => _priceLinks.AsReadOnly();

        public static ElectronicTicket Issue(
            long id,
            long orderId,
            long travelerId,
            long operationId,
            string documentNumber,
            long issuerCarrierId,
            long? issuingOfficeId,
            DocumentAuthority authority,
            DateTimeOffset? voidDeadline,
            int currencyId,
            IReadOnlyList<TicketCouponIssuance> coupons,
            IIdGenerator idGenerator,
            IClock clock)
        {
            if (coupons.Count == 0)
                throw ExceptionFactory.TicketRequiresAtLeastOneCoupon();

            var issuedAt = clock.GetDateTime();
            var total = coupons.Sum(coupon => coupon.IssuanceValue);

            var ticket = new ElectronicTicket(
                id,
                orderId,
                travelerId,
                operationId,
                documentNumber,
                issuerCarrierId,
                issuingOfficeId,
                authority,
                issuedAt,
                voidDeadline,
                total,
                currencyId);

            var couponNumber = 1;

            foreach (var issuance in coupons)
            {
                var coupon = new TicketCoupon(
                    idGenerator.NewId(),
                    id,
                    couponNumber++,
                    issuance.OrderServiceId,
                    issuance.JourneySegmentId,
                    issuance.IssuedSegment,
                    issuance.FareBasis,
                    issuance.IssuanceValue,
                    currencyId);

                ticket._coupons.Add(coupon);

                foreach (var link in issuance.PriceLinks)
                    ticket._priceLinks.Add(new DocumentPriceLink(
                        idGenerator.NewId(),
                        id,
                        coupon.Id,
                        link.PricingLineId,
                        link.AllocationId,
                        link.AttributedValue,
                        currencyId));
            }

            ticket.Causes(new ElectronicTicketIssued(
                idGenerator.NewId().ToString(),
                ticket.Id.ToString(),
                clock.GetDateTime(),
                ticket.Id,
                ticket.OriginalOrderId,
                ticket.TravelerId,
                ticket.OperationId,
                ticket.DocumentNumber,
                ticket.IssuerCarrierId,
                ticket.IssuingOfficeId,
                ticket.Authority,
                ticket.CurrencyId,
                ticket.IssuedTotal,
                ticket.DocumentVersion,
                ticket.PredecessorElectronicTicketId,
                ticket._coupons
                    .Select(coupon => new ElectronicTicketIssuedCoupon(
                        coupon.Id,
                        coupon.CouponNumber,
                        coupon.OrderServiceId,
                        coupon.JourneySegmentId,
                        coupon.IssuanceValue,
                        coupon.PredecessorTicketCouponId))
                    .ToList()));

            return ticket;
        }

        public void RecordProviderConfirmation(string? providerReference, IClock clock)
        {
            ProviderReference = providerReference;
            DocumentVersion++;
            _ = clock;
        }

        public string? ProviderReference { get; private set; }

        public void EnsureCanBeVoided(DateTimeOffset now)
        {
            if (StatusSummary != ElectronicTicketStatus.Issued)
                throw ExceptionFactory.OnlyIssuedDocumentCanBeVoided();

            foreach (var coupon in _coupons)
            {
                if (coupon.FinancialStatus != TicketCouponFinancialStatus.Open)
                    throw ExceptionFactory.CouponFinancialStateForbidsVoid(coupon.CouponNumber, coupon.FinancialStatus);

                if (coupon.ControlStatus != TicketCouponControlStatus.Local)
                    throw ExceptionFactory.CouponControlForbidsVoid(coupon.CouponNumber, coupon.ControlStatus);
            }

            if (VoidDeadline is { } deadline && now > deadline)
                throw ExceptionFactory.DocumentVoidWindowElapsed(DocumentNumber);
        }

        public DocumentVoidRecord? VoidRecord { get; private set; }

        public void Void(
            long operationId,
            VoidReason reason,
            string? reasonDetail,
            long voidedBy,
            string? providerReference,
            IIdGenerator idGenerator,
            IClock clock)
        {
            var now = clock.GetDateTime();

            EnsureCanBeVoided(now);

            foreach (var coupon in _coupons)
                coupon.Void();

            StatusSummary = ElectronicTicketStatus.Voided;
            VoidRecord = new DocumentVoidRecord(operationId, reason, reasonDetail, voidedBy, now, providerReference);
            DocumentVersion++;

            Causes(new ElectronicTicketVoided(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                now,
                Id,
                CurrentServicingOrderId,
                DocumentNumber,
                operationId,
                reason,
                reasonDetail,
                voidedBy,
                now,
                providerReference,
                DocumentVersion));
        }

        public IReadOnlyCollection<long> VoidedServiceIds()
            => _coupons.Select(coupon => coupon.CurrentOrderServiceId).Distinct().ToList();

        public bool CoversService(long orderServiceId)
            => _coupons.Any(coupon => coupon.CurrentOrderServiceId == orderServiceId);

        public IReadOnlyCollection<long> CoveredServiceIds()
            => _coupons.Select(coupon => coupon.CurrentOrderServiceId).Distinct().ToList();

        public IReadOnlyCollection<long> CarriedPricingLineIds()
            => _priceLinks.Select(link => link.PricingLineId).Distinct().ToList();

        public IReadOnlyList<CarriedPricingLink> CarriedPricingLinks()
            => _priceLinks
                .Select(link => new CarriedPricingLink(
                    link.PricingLineId,
                    link.CouponId is { } couponId ? CouponFor(couponId).CouponNumber : 0,
                    link.AttributedValue))
                .OrderBy(link => link.CouponNumber)
                .ThenBy(link => link.PricingLineId)
                .ToList();

        public IReadOnlyCollection<DocumentRefundRecord> Refunds => _refunds.AsReadOnly();

        public bool IsFullyUnused => _coupons.All(coupon => coupon.FinancialStatus == TicketCouponFinancialStatus.Open);

        public IReadOnlyCollection<long> RefundableCouponIds()
            => _coupons
                .Where(coupon => coupon.FinancialStatus == TicketCouponFinancialStatus.Open)
                .Select(coupon => coupon.Id)
                .Order()
                .ToList();

        public void EnsureRefundScopeIsEligible(IReadOnlyCollection<long> couponIds)
        {
            if (StatusSummary is ElectronicTicketStatus.Refunded
                or ElectronicTicketStatus.Voided
                or ElectronicTicketStatus.Exchanged)
                throw ExceptionFactory.DocumentNotRefundable(DocumentNumber, StatusSummary);

            if (StatusSummary is not (ElectronicTicketStatus.Issued or ElectronicTicketStatus.PartiallyUsed))
                throw ExceptionFactory.PartialRefundNotSupported(DocumentNumber, StatusSummary);

            if (couponIds.Count == 0)
                throw ExceptionFactory.RefundScopeIsEmpty(DocumentNumber);

            foreach (var couponId in couponIds)
            {
                var coupon = _coupons.FirstOrDefault(candidate => candidate.Id == couponId)
                             ?? throw ExceptionFactory.RefundScopeCouponNotOnDocument(couponId, DocumentNumber);

                if (coupon.FinancialStatus != TicketCouponFinancialStatus.Open)
                    throw ExceptionFactory.CouponIsNotRefundable(coupon.CouponNumber, coupon.FinancialStatus);

                if (coupon.ControlStatus != TicketCouponControlStatus.Local)
                    throw ExceptionFactory.CouponControlForbidsRefund(coupon.CouponNumber, coupon.ControlStatus);
            }
        }

        public IReadOnlyCollection<long> ServiceIdsClosedByRefundOf(IReadOnlyCollection<long> couponIds)
        {
            var surviving = _coupons
                .Where(coupon => !couponIds.Contains(coupon.Id))
                .Where(coupon => coupon.FinancialStatus
                    is TicketCouponFinancialStatus.Open
                    or TicketCouponFinancialStatus.Used)
                .Select(coupon => coupon.CurrentOrderServiceId)
                .ToHashSet();

            return _coupons
                .Where(coupon => couponIds.Contains(coupon.Id))
                .Select(coupon => coupon.CurrentOrderServiceId)
                .Distinct()
                .Where(serviceId => !surviving.Contains(serviceId))
                .ToList();
        }

        private ElectronicTicketStatus DeriveStatusSummary()
        {
            if (_coupons.Any(coupon => coupon.FinancialStatus == TicketCouponFinancialStatus.Open))
                return _coupons.Any(coupon => coupon.FinancialStatus != TicketCouponFinancialStatus.Open)
                    ? ElectronicTicketStatus.PartiallyUsed
                    : ElectronicTicketStatus.Issued;

            return _coupons.All(coupon => coupon.FinancialStatus == TicketCouponFinancialStatus.Used)
                ? ElectronicTicketStatus.Used
                : ElectronicTicketStatus.Refunded;
        }

        public DocumentRefundRecord Refund(
            long operationId,
            IReadOnlyCollection<long> couponIds,
            RefundProvenance provenance,
            string? providerReference,
            long? refundedBy,
            string? actorScope,
            IIdGenerator idGenerator,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(provenance);

            EnsureRefundScopeIsEligible(couponIds);

            if (provenance.ApprovedAmount < 0m)
                throw ExceptionFactory.RefundAmountMustBeNonNegative(provenance.ApprovedAmount);

            var record = new DocumentRefundRecord(
                idGenerator.NewId(),
                Id,
                operationId,
                provenance.QuotedRefundId,
                provenance.PricingSource,
                provenance.SourcePricingReference,
                provenance.SourceRefundType,
                provenance.SourceEvidence,
                provenance.ManualAuthority,
                provenance.ApprovedAmount,
                CurrencyId,
                provenance.ApprovedDisposition,
                provenance.DispositionReference,
                providerReference,
                refundedBy,
                actorScope,
                clock.GetDateTime());

            foreach (var coupon in _coupons.Where(candidate => couponIds.Contains(candidate.Id)))
            {
                coupon.Refund();
                record.AddCoupon(idGenerator.NewId(), coupon.Id, coupon.CouponNumber, coupon.CurrentOrderServiceId);
            }

            _refunds.Add(record);

            StatusSummary = DeriveStatusSummary();
            DocumentVersion++;

            Causes(new ElectronicTicketRefunded(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                record.RefundedAt,
                Id,
                CurrentServicingOrderId,
                DocumentNumber,
                operationId,
                record.Id,
                record.QuotedRefundId,
                record.PricingSource,
                record.ApprovedAmount,
                record.CurrencyId,
                record.ApprovedDisposition,
                record.Coupons.Select(coupon => coupon.TicketCouponId).ToList(),
                StatusSummary,
                DocumentVersion));

            return record;
        }

        public DocumentRefundRecord? RefundOf(long operationId)
            => _refunds.FirstOrDefault(record => record.OperationId == operationId);

        public IReadOnlyCollection<DocumentRefundCorrectionRecord> RefundCorrections => _refundCorrections.AsReadOnly();

        public IReadOnlyCollection<DocumentRevalidationRecord> Revalidations => _revalidations.AsReadOnly();

        public long? PredecessorElectronicTicketId { get; private set; }

        public long? PredecessorExchangeOperationId { get; private set; }

        public IReadOnlyCollection<DocumentExchangeRecord> Exchanges => _exchanges.AsReadOnly();

        public DocumentExchangeRecord? ExchangeOf(long operationId)
            => _exchanges.FirstOrDefault(record => record.OperationId == operationId);

        public void EnsureCanBeExchanged(IReadOnlyList<ExchangeCouponScope> scope)
        {
            ArgumentNullException.ThrowIfNull(scope);

            if (_exchanges.FirstOrDefault() is { } prior)
                throw ExceptionFactory.DocumentAlreadyExchanged(DocumentNumber, prior.SuccessorDocumentNumber);

            if (StatusSummary != ElectronicTicketStatus.Issued)
                throw ExceptionFactory.DocumentNotExchangeable(DocumentNumber, StatusSummary);

            var scopedCouponIds = scope.Select(item => item.TicketCouponId).ToHashSet();

            if (scopedCouponIds.Count != scope.Count
                || scopedCouponIds.Count != _coupons.Count
                || _coupons.Any(coupon => !scopedCouponIds.Contains(coupon.Id)))
                throw ExceptionFactory.ExchangeCouponScopeIncomplete(DocumentNumber, _coupons.Count, scope.Count);

            foreach (var item in scope)
            {
                var coupon = CouponFor(item.TicketCouponId);

                if (coupon.CurrentOrderServiceId != item.OrderServiceId)
                    throw ExceptionFactory.ChangeCouponDoesNotCoverTheService(coupon.CouponNumber, item.OrderServiceId);

                if (coupon.FinancialStatus != TicketCouponFinancialStatus.Open)
                    throw ExceptionFactory.ExchangeRequiresFullyUnusedTicket(
                        coupon.CouponNumber, DocumentNumber, coupon.FinancialStatus);

                if (coupon.ControlStatus != TicketCouponControlStatus.Local)
                    throw ExceptionFactory.CouponControlForbidsExchange(coupon.CouponNumber, coupon.ControlStatus);
            }
        }

        public DocumentExchangeRecord MarkExchanged(
            ExchangeProvenance provenance,
            long successorTicketId,
            string successorDocumentNumber,
            IReadOnlyList<ExchangedCouponLineage> coupons,
            IIdGenerator idGenerator,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(provenance);
            ArgumentNullException.ThrowIfNull(coupons);

            EnsureCanBeExchanged(coupons
                .Select(lineage => new ExchangeCouponScope(
                    lineage.PredecessorTicketCouponId,
                    CouponFor(lineage.PredecessorTicketCouponId).CurrentOrderServiceId))
                .ToList());

            var record = new DocumentExchangeRecord(
                idGenerator.NewId(),
                Id,
                successorTicketId,
                successorDocumentNumber,
                provenance.OperationId,
                provenance.QuotedExchangeId,
                provenance.TargetSelectionRef,
                provenance.SourcePricingReference,
                provenance.ProviderReference,
                provenance.ActorId,
                provenance.ActorScope,
                clock.GetDateTime());

            var exchanged = new List<ElectronicTicketExchangedCoupon>();

            foreach (var lineage in coupons.OrderBy(lineage => CouponFor(lineage.PredecessorTicketCouponId).CouponNumber))
            {
                var coupon = CouponFor(lineage.PredecessorTicketCouponId);

                record.AddCoupon(
                    idGenerator.NewId(),
                    coupon.Id,
                    coupon.CouponNumber,
                    lineage.SuccessorTicketCouponId,
                    lineage.SuccessorCouponNumber,
                    coupon.CurrentOrderServiceId,
                    lineage.SuccessorOrderServiceId);

                exchanged.Add(new ElectronicTicketExchangedCoupon(
                    coupon.Id,
                    coupon.CouponNumber,
                    lineage.SuccessorTicketCouponId,
                    lineage.SuccessorCouponNumber,
                    coupon.CurrentOrderServiceId,
                    lineage.SuccessorOrderServiceId));

                coupon.MarkExchanged();
            }

            _exchanges.Add(record);

            StatusSummary = ElectronicTicketStatus.Exchanged;
            DocumentVersion++;

            Causes(new ElectronicTicketExchanged(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                record.ExchangedAt,
                Id,
                CurrentServicingOrderId,
                DocumentNumber,
                provenance.OperationId,
                record.Id,
                successorTicketId,
                successorDocumentNumber,
                exchanged,
                provenance.QuotedExchangeId,
                provenance.TargetSelectionRef,
                DocumentVersion));

            return record;
        }

        public static ElectronicTicket IssueSuccessor(SuccessorTicketIssuance issuance, IIdGenerator idGenerator, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(issuance);

            if (issuance.Coupons.Count == 0)
                throw ExceptionFactory.TicketRequiresAtLeastOneCoupon();

            if (issuance.Coupons.Select(coupon => coupon.CouponNumber).Distinct().Count() != issuance.Coupons.Count)
                throw ExceptionFactory.ExchangeCouponScopeIncomplete(
                    issuance.DocumentNumber, issuance.Coupons.Count, issuance.Coupons.Select(coupon => coupon.CouponNumber).Distinct().Count());

            var ticket = new ElectronicTicket(
                issuance.TicketId,
                issuance.OrderId,
                issuance.TravelerId,
                issuance.ExchangeOperationId,
                issuance.DocumentNumber,
                issuance.IssuerCarrierId,
                issuance.IssuingOfficeId,
                issuance.Authority,
                clock.GetDateTime(),
                issuance.VoidDeadline,
                issuance.Coupons.Sum(coupon => coupon.IssuanceValue),
                issuance.CurrencyId)
            {
                PredecessorElectronicTicketId = issuance.PredecessorTicketId,
                PredecessorExchangeOperationId = issuance.ExchangeOperationId
            };

            foreach (var issued in issuance.Coupons.OrderBy(coupon => coupon.CouponNumber))
            {
                var coupon = new TicketCoupon(
                    issued.CouponId,
                    issuance.TicketId,
                    issued.CouponNumber,
                    issued.OrderServiceId,
                    issued.JourneySegmentId,
                    issued.IssuedSegment,
                    issued.FareBasis,
                    issued.IssuanceValue,
                    issuance.CurrencyId,
                    issued.PredecessorTicketCouponId);

                ticket._coupons.Add(coupon);

                foreach (var link in issued.PriceLinks)
                    ticket._priceLinks.Add(new DocumentPriceLink(
                        idGenerator.NewId(),
                        issuance.TicketId,
                        coupon.Id,
                        link.PricingLineId,
                        link.AllocationId,
                        link.AttributedValue,
                        issuance.CurrencyId));
            }

            ticket.Causes(new ElectronicTicketIssued(
                idGenerator.NewId().ToString(),
                ticket.Id.ToString(),
                clock.GetDateTime(),
                ticket.Id,
                ticket.OriginalOrderId,
                ticket.TravelerId,
                ticket.OperationId,
                ticket.DocumentNumber,
                ticket.IssuerCarrierId,
                ticket.IssuingOfficeId,
                ticket.Authority,
                ticket.CurrencyId,
                ticket.IssuedTotal,
                ticket.DocumentVersion,
                ticket.PredecessorElectronicTicketId,
                ticket._coupons
                    .Select(coupon => new ElectronicTicketIssuedCoupon(
                        coupon.Id,
                        coupon.CouponNumber,
                        coupon.OrderServiceId,
                        coupon.JourneySegmentId,
                        coupon.IssuanceValue,
                        coupon.PredecessorTicketCouponId))
                    .ToList()));

            return ticket;
        }

        public DocumentRevalidationRecord? RevalidationOf(long operationId)
            => _revalidations.FirstOrDefault(record => record.OperationId == operationId);

        public TicketCoupon CouponFor(long ticketCouponId)
            => _coupons.FirstOrDefault(coupon => coupon.Id == ticketCouponId)
               ?? throw ExceptionFactory.RefundScopeCouponNotOnDocument(ticketCouponId, DocumentNumber);

        public void EnsureCouponCanBeRevalidated(long ticketCouponId, long currentOrderServiceId)
        {
            if (StatusSummary is not (ElectronicTicketStatus.Issued or ElectronicTicketStatus.PartiallyUsed))
                throw ExceptionFactory.DocumentChangeNotPermitted(DocumentNumber);

            var coupon = CouponFor(ticketCouponId);

            if (coupon.CurrentOrderServiceId != currentOrderServiceId)
                throw ExceptionFactory.ChangeCouponDoesNotCoverTheService(coupon.CouponNumber, currentOrderServiceId);

            if (coupon.FinancialStatus != TicketCouponFinancialStatus.Open)
                throw ExceptionFactory.CouponStateForbidsChange(coupon.CouponNumber, coupon.FinancialStatus);

            if (coupon.ControlStatus != TicketCouponControlStatus.Local)
                throw ExceptionFactory.CouponControlForbidsRefund(coupon.CouponNumber, coupon.ControlStatus);
        }

        public DocumentRevalidationRecord Revalidate(
            long operationId,
            long ticketCouponId,
            long replacementOrderServiceId,
            string quotedChangeId,
            string targetSelectionRef,
            string? providerReference,
            long? revalidatedBy,
            string? actorScope,
            IIdGenerator idGenerator,
            IClock clock)
        {
            var coupon = CouponFor(ticketCouponId);

            EnsureCouponCanBeRevalidated(ticketCouponId, coupon.CurrentOrderServiceId);

            var record = new DocumentRevalidationRecord(
                idGenerator.NewId(),
                Id,
                operationId,
                quotedChangeId,
                targetSelectionRef,
                coupon.Id,
                coupon.CouponNumber,
                coupon.CurrentOrderServiceId,
                replacementOrderServiceId,
                providerReference,
                revalidatedBy,
                actorScope,
                clock.GetDateTime());

            coupon.RebindToService(replacementOrderServiceId);

            _revalidations.Add(record);

            DocumentVersion++;

            Causes(new ElectronicTicketRevalidated(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                record.RevalidatedAt,
                Id,
                CurrentServicingOrderId,
                DocumentNumber,
                operationId,
                record.Id,
                record.TicketCouponId,
                record.CouponNumber,
                record.PreviousOrderServiceId,
                record.NewOrderServiceId,
                record.QuotedChangeId,
                record.TargetSelectionRef,
                DocumentVersion));

            return record;
        }

        public DocumentRefundCorrectionRecord? RefundCorrectionOf(long operationId)
            => _refundCorrections.FirstOrDefault(record => record.OperationId == operationId);

        public DocumentRefundCorrectionRecord? CorrectionForRefund(long documentRefundRecordId)
            => _refundCorrections.FirstOrDefault(record => record.DocumentRefundRecordId == documentRefundRecordId);

        public DocumentRefundRecord RefundRecord(long documentRefundRecordId)
            => _refunds.FirstOrDefault(record => record.Id == documentRefundRecordId)
               ?? throw ExceptionFactory.RefundRecordNotFound(documentRefundRecordId, DocumentNumber);

        public void EnsureRefundCanBeCancelled(long documentRefundRecordId)
        {
            var record = RefundRecord(documentRefundRecordId);

            if (CorrectionForRefund(documentRefundRecordId) is { } existing)
                throw ExceptionFactory.RefundAlreadyCancelled(documentRefundRecordId, existing.OperationId);

            if (record.ValueMovementStatus != ProviderOperationOutcome.Confirmed)
                throw ExceptionFactory.RefundValueNotSettledForCorrection(
                    documentRefundRecordId, record.ValueMovementStatus);

            foreach (var refunded in record.Coupons)
            {
                var coupon = _coupons.FirstOrDefault(candidate => candidate.Id == refunded.TicketCouponId)
                             ?? throw ExceptionFactory.RefundScopeCouponNotOnDocument(
                                 refunded.TicketCouponId, DocumentNumber);

                if (coupon.FinancialStatus != TicketCouponFinancialStatus.Refunded)
                    throw ExceptionFactory.CouponStateForbidsRefundCorrection(
                        coupon.CouponNumber, coupon.FinancialStatus);

                if (coupon.ControlStatus != TicketCouponControlStatus.Local)
                    throw ExceptionFactory.CouponControlForbidsRefund(coupon.CouponNumber, coupon.ControlStatus);
            }
        }

        public IReadOnlyList<int> RefundedCouponNumbers(long documentRefundRecordId)
            => RefundRecord(documentRefundRecordId).Coupons
                .Select(coupon => coupon.CouponNumber)
                .Order()
                .ToList();

        public DocumentRefundCorrectionRecord CancelRefund(
            long documentRefundRecordId,
            long operationId,
            string reason,
            string? reasonDetail,
            string? providerReference,
            long? correctedBy,
            string? actorScope,
            IIdGenerator idGenerator,
            IClock clock)
        {
            EnsureRefundCanBeCancelled(documentRefundRecordId);

            var refund = RefundRecord(documentRefundRecordId);

            var correction = new DocumentRefundCorrectionRecord(
                idGenerator.NewId(),
                Id,
                refund.Id,
                operationId,
                refund.OperationId,
                refund.ApprovedAmount,
                CurrencyId,
                reason,
                reasonDetail,
                providerReference,
                correctedBy,
                actorScope,
                clock.GetDateTime());

            foreach (var refunded in refund.Coupons)
            {
                var coupon = _coupons.Single(candidate => candidate.Id == refunded.TicketCouponId);

                coupon.RestoreFromRefund();
                correction.AddCoupon(
                    idGenerator.NewId(),
                    coupon.Id,
                    coupon.CouponNumber,
                    refunded.OrderServiceId);
            }

            _refundCorrections.Add(correction);

            StatusSummary = DeriveStatusSummary();
            DocumentVersion++;

            Causes(new ElectronicTicketRefundCancelled(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                correction.CorrectedAt,
                Id,
                CurrentServicingOrderId,
                DocumentNumber,
                operationId,
                documentRefundRecordId,
                correction.Id,
                correction.OriginalRefundOperationId,
                correction.CorrectedAmount,
                correction.CurrencyId,
                correction.Reason,
                StatusSummary,
                DocumentVersion));

            return correction;
        }

        public void AttachRefundCorrectionPriceChangeSet(long operationId, long priceChangeSetId)
            => RefundCorrectionOf(operationId)?.AttachPriceChangeSet(priceChangeSetId);

        public void RecordRefundCorrectionValueMovement(
            long operationId,
            ProviderOperationOutcome outcome,
            string? reference,
            string? detail,
            IClock clock)
            => RefundCorrectionOf(operationId)?.RecordValueCorrection(outcome, reference, detail, clock.GetDateTime());

        public IReadOnlyCollection<long> RefundedServiceIds()
            => _coupons
                .Where(coupon => coupon.FinancialStatus == TicketCouponFinancialStatus.Refunded)
                .Select(coupon => coupon.CurrentOrderServiceId)
                .Distinct()
                .ToList();

        public void AttachRefundPriceChangeSet(long operationId, long priceChangeSetId)
            => RefundOf(operationId)?.AttachPriceChangeSet(priceChangeSetId);

        public void RecordRefundValueMovement(
            long operationId,
            ProviderOperationOutcome outcome,
            string? reference,
            string? detail,
            IClock clock)
            => RefundOf(operationId)?.RecordValueMovement(outcome, reference, detail, clock.GetDateTime());
    }

    public sealed record TicketCouponIssuance(
        long OrderServiceId,
        long JourneySegmentId,
        IssuedSegmentSnapshot IssuedSegment,
        string? FareBasis,
        decimal IssuanceValue,
        IReadOnlyList<TicketCouponPriceLink> PriceLinks);

    public sealed record TicketCouponPriceLink(long PricingLineId, long? AllocationId, decimal AttributedValue);
}
