using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities
{
    public sealed class EmdCoupon : Entity<long>
    {
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
            int currencyId)
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

        internal void Void() => Status = EmdCouponStatus.Void;
    }
}
