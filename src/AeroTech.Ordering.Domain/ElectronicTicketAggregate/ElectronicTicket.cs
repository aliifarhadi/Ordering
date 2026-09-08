using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate
{
    public sealed class ElectronicTicket : AggregateRoot<long>
    {
        private readonly List<TicketCoupon> _coupons = new();
        private readonly List<DocumentPriceLink> _priceLinks = new();

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

        public void Void(IClock clock)
        {
            if (StatusSummary != ElectronicTicketStatus.Issued)
                throw ExceptionFactory.OnlyIssuedDocumentCanBeVoided();

            foreach (var coupon in _coupons)
                coupon.Void();

            StatusSummary = ElectronicTicketStatus.Voided;
            DocumentVersion++;
            _ = clock;
        }

        public bool CoversService(long orderServiceId)
            => _coupons.Any(coupon => coupon.CurrentOrderServiceId == orderServiceId);
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
