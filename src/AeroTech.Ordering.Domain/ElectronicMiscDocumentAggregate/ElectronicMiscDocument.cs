using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.DomainEvents;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate
{
    public sealed class ElectronicMiscDocument : AggregateRoot<long>
    {
        private readonly List<EmdCoupon> _coupons = new();
        private readonly List<EmdPriceLink> _priceLinks = new();

        private ElectronicMiscDocument()
        {
        }

        private ElectronicMiscDocument(
            long id,
            long originalOrderId,
            long? travelerId,
            long operationId,
            string documentNumber,
            ElectronicMiscDocumentType type,
            string reasonForIssuanceCode,
            long issuerCarrierId,
            long? issuingOfficeId,
            DocumentAuthority authority,
            DateTimeOffset issuedAt,
            decimal issuedTotal,
            int currencyId)
        {
            Id = id;
            OriginalOrderId = originalOrderId;
            CurrentServicingOrderId = originalOrderId;
            TravelerId = travelerId;
            OperationId = operationId;
            DocumentNumber = documentNumber;
            Type = type;
            ReasonForIssuanceCode = reasonForIssuanceCode;
            IssuerCarrierId = issuerCarrierId;
            IssuingOfficeId = issuingOfficeId;
            Authority = authority;
            IssuedAt = issuedAt;
            IssuedTotal = issuedTotal;
            CurrencyId = currencyId;
            StatusSummary = ElectronicMiscDocumentStatus.Issued;
            DocumentVersion = 1;
        }

        public long OriginalOrderId { get; private set; }

        public long CurrentServicingOrderId { get; private set; }

        public long? TravelerId { get; private set; }

        public long OperationId { get; private set; }

        public string DocumentNumber { get; private set; } = default!;

        public ElectronicMiscDocumentType Type { get; private set; }

        public string ReasonForIssuanceCode { get; private set; } = default!;

        public long IssuerCarrierId { get; private set; }

        public long? IssuingOfficeId { get; private set; }

        public DocumentAuthority Authority { get; private set; }

        public DateTimeOffset IssuedAt { get; private set; }

        public decimal IssuedTotal { get; private set; }

        public int CurrencyId { get; private set; }

        public string? ProviderReference { get; private set; }

        public ElectronicMiscDocumentStatus StatusSummary { get; private set; }

        public int DocumentVersion { get; private set; }

        public IReadOnlyCollection<EmdCoupon> Coupons => _coupons.AsReadOnly();

        public IReadOnlyCollection<EmdPriceLink> PriceLinks => _priceLinks.AsReadOnly();

        public bool IsAssociated => Type == ElectronicMiscDocumentType.Associated;

        public IReadOnlyList<EmdCoupon> CouponsAssociatedWith(IReadOnlyCollection<long> ticketCouponIds)
        {
            ArgumentNullException.ThrowIfNull(ticketCouponIds);

            return IsAssociated && StatusSummary != ElectronicMiscDocumentStatus.Voided
                ? _coupons
                    .Where(coupon => coupon.IsOpenForUse
                                     && coupon.AssociatedTicketCouponId is { } associated
                                     && ticketCouponIds.Contains(associated))
                    .OrderBy(coupon => coupon.CouponNumber)
                    .ToList()
                : [];
        }

        public bool PermitsDisassociation(int emdCouponNumber, long operationId, long predecessorTicketCouponId)
            => Associable(emdCouponNumber) is { } candidate
               && (candidate.IsAssociatedWith(predecessorTicketCouponId)
                   || candidate.IsDisassociatedByReissue(operationId)
                   || candidate.IsReassociatedBy(operationId));

        public bool PermitsReassociation(int emdCouponNumber, long operationId, long successorTicketCouponId)
            => Associable(emdCouponNumber) is { } candidate
               && (candidate.IsDisassociatedByReissue(operationId)
                   || candidate.IsAssociatedWith(successorTicketCouponId));

        private EmdCoupon? Associable(int emdCouponNumber)
            => IsAssociated
               && StatusSummary != ElectronicMiscDocumentStatus.Voided
               && _coupons.SingleOrDefault(coupon => coupon.CouponNumber == emdCouponNumber) is { IsOpenForUse: true } candidate
                ? candidate
                : null;

        public void DisassociateCouponByReissue(
            EmdCouponDisassociation disassociation,
            IIdGenerator idGenerator,
            IClock clock)
        {
            ArgumentNullException.ThrowIfNull(disassociation);

            if (!IsAssociated)
                throw ExceptionFactory.ElectronicMiscDocumentIsNotAssociable(DocumentNumber, Type);

            var coupon = RequireCoupon(disassociation.EmdCouponNumber);

            if (!coupon.IsOpenForUse)
                throw ExceptionFactory.ElectronicMiscDocumentCouponIsNotAssociable(
                    DocumentNumber, coupon.CouponNumber, coupon.Status);

            if (coupon.IsDisassociatedByReissue(disassociation.OperationId)
                || coupon.IsReassociatedBy(disassociation.OperationId))
                return;

            if (!coupon.IsAssociatedWith(disassociation.PredecessorTicketCouponId))
                throw ExceptionFactory.ElectronicMiscDocumentAssociationMoved(
                    DocumentNumber, coupon.CouponNumber, disassociation.PredecessorDocumentNumber);

            coupon.DisassociateByReissue(disassociation, idGenerator, clock.GetDateTime());

            DocumentVersion++;
        }

        public EmdCoupon RequireCoupon(int couponNumber)
            => _coupons.SingleOrDefault(coupon => coupon.CouponNumber == couponNumber)
               ?? throw ExceptionFactory.ElectronicMiscDocumentCouponNotFound(DocumentNumber, couponNumber);

        public void ReassociateCoupon(EmdCouponReassociation reassociation, IIdGenerator idGenerator, IClock clock)
        {
            ArgumentNullException.ThrowIfNull(reassociation);

            if (!IsAssociated)
                throw ExceptionFactory.ElectronicMiscDocumentIsNotAssociable(DocumentNumber, Type);

            var coupon = RequireCoupon(reassociation.EmdCouponNumber);

            if (!coupon.IsOpenForUse)
                throw ExceptionFactory.ElectronicMiscDocumentCouponIsNotAssociable(
                    DocumentNumber, coupon.CouponNumber, coupon.Status);

            if (coupon.IsAssociatedWith(reassociation.SuccessorTicketCouponId))
                return;

            if (!coupon.IsDisassociatedByReissue(reassociation.OperationId))
                throw ExceptionFactory.ElectronicMiscDocumentAssociationMoved(
                    DocumentNumber, coupon.CouponNumber, reassociation.PredecessorDocumentNumber);

            coupon.Reassociate(reassociation, idGenerator, clock.GetDateTime());

            DocumentVersion++;
        }

        public static ElectronicMiscDocument Issue(
            long id,
            long orderId,
            long? travelerId,
            long operationId,
            string documentNumber,
            ElectronicMiscDocumentType type,
            string reasonForIssuanceCode,
            long issuerCarrierId,
            long? issuingOfficeId,
            DocumentAuthority authority,
            int currencyId,
            IReadOnlyList<EmdCouponIssuance> coupons,
            IIdGenerator idGenerator,
            IClock clock)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(documentNumber);

            if (coupons.Count == 0)
                throw ExceptionFactory.MiscellaneousDocumentRequiresCoupon();

            if (string.IsNullOrWhiteSpace(reasonForIssuanceCode))
                throw ExceptionFactory.ReasonForIssuanceCodeRequired();

            var document = new ElectronicMiscDocument(
                id,
                orderId,
                travelerId,
                operationId,
                documentNumber,
                type,
                reasonForIssuanceCode.Trim(),
                issuerCarrierId,
                issuingOfficeId,
                authority,
                clock.GetDateTime(),
                coupons.Sum(coupon => coupon.IssuanceValue),
                currencyId);

            var couponNumber = 1;

            foreach (var issuance in coupons)
            {
                EnsureCouponIsWellFormed(type, couponNumber, issuance);

                var coupon = new EmdCoupon(
                    idGenerator.NewId(),
                    id,
                    couponNumber++,
                    issuance.Purpose,
                    issuance.ReasonForIssuanceSubCode.Trim(),
                    issuance.OrderServiceId,
                    issuance.PricingLineId,
                    issuance.ExternalValueReference,
                    issuance.AssociatedTicketCouponId,
                    issuance.IssuanceValue,
                    currencyId);

                coupon.RecordIssuedAssociation(operationId, idGenerator, document.IssuedAt);

                document._coupons.Add(coupon);

                foreach (var link in issuance.PriceLinks)
                    document._priceLinks.Add(new EmdPriceLink(
                        idGenerator.NewId(),
                        id,
                        coupon.Id,
                        link.PricingLineId,
                        link.AllocationId,
                        link.AttributedValue,
                        currencyId));
            }

            document.Causes(new ElectronicMiscDocumentIssued(
                idGenerator.NewId().ToString(),
                document.Id.ToString(),
                document.IssuedAt,
                document.Id,
                document.OriginalOrderId,
                document.TravelerId,
                document.OperationId,
                document.DocumentNumber,
                document.Type,
                document.ReasonForIssuanceCode,
                document.IssuerCarrierId,
                document.IssuingOfficeId,
                document.Authority,
                document.CurrencyId,
                document.IssuedTotal,
                document.DocumentVersion,
                document._coupons
                    .Select(coupon => new ElectronicMiscDocumentIssuedCoupon(
                        coupon.Id,
                        coupon.CouponNumber,
                        coupon.Purpose,
                        coupon.OrderServiceId,
                        coupon.AssociatedTicketCouponId,
                        coupon.IssuanceValue))
                    .ToList()));

            return document;
        }

        public void RecordProviderConfirmation(string? providerReference)
        {
            ProviderReference = providerReference;
            DocumentVersion++;
        }

        public void EnsureCanBeVoided()
        {
            if (StatusSummary != ElectronicMiscDocumentStatus.Issued)
                throw ExceptionFactory.OnlyIssuedDocumentCanBeVoided();

            foreach (var coupon in _coupons.Where(coupon => coupon.Status != EmdCouponStatus.OpenForUse))
                throw ExceptionFactory.EmdCouponStateForbidsVoid(coupon.CouponNumber, coupon.Status);
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
            EnsureCanBeVoided();

            var now = clock.GetDateTime();

            foreach (var coupon in _coupons)
                coupon.Void();

            StatusSummary = ElectronicMiscDocumentStatus.Voided;
            VoidRecord = new DocumentVoidRecord(
                operationId,
                reason,
                reasonDetail,
                voidedBy,
                now,
                providerReference);

            DocumentVersion++;

            Causes(new ElectronicMiscDocumentVoided(
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
            => _coupons
                .Where(coupon => coupon.OrderServiceId is not null)
                .Select(coupon => coupon.OrderServiceId!.Value)
                .Distinct()
                .ToList();

        public bool DocumentsService(long orderServiceId)
            => _coupons.Any(coupon => coupon.OrderServiceId == orderServiceId);

        private static void EnsureCouponIsWellFormed(
            ElectronicMiscDocumentType type,
            int couponNumber,
            EmdCouponIssuance issuance)
        {
            if (string.IsNullOrWhiteSpace(issuance.ReasonForIssuanceSubCode))
                throw ExceptionFactory.ReasonForIssuanceSubCodeRequired(couponNumber);

            if (issuance.IssuanceValue < 0m)
                throw ExceptionFactory.EmdCouponValueMustBeNonNegative();

            if (type == ElectronicMiscDocumentType.Associated && issuance.AssociatedTicketCouponId is null)
                throw ExceptionFactory.AssociatedDocumentRequiresTicketCoupon(couponNumber);

            if (type == ElectronicMiscDocumentType.Standalone && issuance.AssociatedTicketCouponId is not null)
                throw ExceptionFactory.StandaloneDocumentCannotAssociateTicketCoupon(couponNumber);

            switch (issuance.Purpose)
            {
                case EmdCouponPurpose.Service when issuance.OrderServiceId is null:
                    throw ExceptionFactory.ServiceCouponRequiresOrderService();

                case EmdCouponPurpose.Fee when issuance.PricingLineId is null || issuance.OrderServiceId is not null:
                    throw ExceptionFactory.FeeCouponRequiresPricingLine();

                case EmdCouponPurpose.Deposit or EmdCouponPurpose.ResidualValue
                    when string.IsNullOrWhiteSpace(issuance.ExternalValueReference):
                    throw ExceptionFactory.ValueCouponRequiresExternalReference(issuance.Purpose);
            }
        }
    }

    public sealed record EmdCouponIssuance(
        EmdCouponPurpose Purpose,
        string ReasonForIssuanceSubCode,
        decimal IssuanceValue,
        IReadOnlyList<EmdCouponPriceLink> PriceLinks,
        long? OrderServiceId = null,
        long? PricingLineId = null,
        string? ExternalValueReference = null,
        long? AssociatedTicketCouponId = null);

    public sealed record EmdCouponPriceLink(long PricingLineId, long? AllocationId, decimal AttributedValue);
}
