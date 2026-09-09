using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate
{
    public sealed class ElectronicTicket : AggregateRoot<long>
    {
        private readonly List<TicketCoupon> _coupons = new();
        private readonly List<DocumentPriceLink> _priceLinks = new();
        private readonly List<DocumentRefundRecord> _refunds = new();

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
            IClock clock)
        {
            var now = clock.GetDateTime();

            EnsureCanBeVoided(now);

            foreach (var coupon in _coupons)
                coupon.Void();

            StatusSummary = ElectronicTicketStatus.Voided;
            VoidRecord = new DocumentVoidRecord(operationId, reason, reasonDetail, voidedBy, now, providerReference);
            DocumentVersion++;
        }

        public IReadOnlyCollection<long> VoidedServiceIds()
            => _coupons.Select(coupon => coupon.CurrentOrderServiceId).Distinct().ToList();

        public bool CoversService(long orderServiceId)
            => _coupons.Any(coupon => coupon.CurrentOrderServiceId == orderServiceId);

        public IReadOnlyCollection<long> CoveredServiceIds()
            => _coupons.Select(coupon => coupon.CurrentOrderServiceId).Distinct().ToList();

        public IReadOnlyCollection<long> CarriedPricingLineIds()
            => _priceLinks.Select(link => link.PricingLineId).Distinct().ToList();

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
            AcceptedRefundProvenance provenance,
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

            return record;
        }

        public DocumentRefundRecord? RefundOf(long operationId)
            => _refunds.FirstOrDefault(record => record.OperationId == operationId);

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
