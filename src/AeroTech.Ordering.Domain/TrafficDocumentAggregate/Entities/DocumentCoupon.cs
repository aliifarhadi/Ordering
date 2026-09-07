using AeroTech.Ordering.Domain.TrafficDocumentAggregate.ValueObjects;
using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities
{
    public abstract class DocumentCoupon : Entity<long>
    {
        protected DocumentCoupon()
        {
        }

        protected DocumentCoupon(
            long id,
            long trafficDocumentId,
            long orderServiceId,
            int couponNumber,
            DocumentAmounts amounts)
        {
            Id = id;
            TrafficDocumentId = trafficDocumentId;
            OrderServiceId = orderServiceId;
            CouponNumber = couponNumber;
            CouponUniqueCode = Guid.NewGuid().ToString("N");
            Status = CouponStatus.OpenForUse;
            Fare = amounts.Fare;
            TaxesTotal = amounts.TaxesTotal;
            FeesTotal = amounts.FeesTotal;
            Commission = amounts.Commission;
            TotalAmount = amounts.TotalAmount;
            CurrencyId = amounts.CurrencyId;
        }

        public long TrafficDocumentId { get; private set; }

        public long OrderServiceId { get; private set; }

        public int CouponNumber { get; private set; }

        public string CouponUniqueCode { get; private set; } = default!;

        public CouponStatus Status { get; protected set; }

        public decimal Fare { get; private set; }

        public decimal TaxesTotal { get; private set; }

        public decimal FeesTotal { get; private set; }

        public decimal Commission { get; private set; }

        public decimal TotalAmount { get; private set; }

        public int CurrencyId { get; private set; }

        public void Void() => Status = CouponStatus.Voided;

        public void Cancel() => Status = CouponStatus.Cancel;

        internal void ReassignService(long newOrderServiceId) => OrderServiceId = newOrderServiceId;
    }
}
