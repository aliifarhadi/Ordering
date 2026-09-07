using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities
{
    public sealed class TicketCoupon : Entity<long>
    {
        private TicketCoupon()
        {
        }

        internal TicketCoupon(
            long id,
            long ticketId,
            int couponNumber,
            long orderServiceId,
            long journeySegmentId,
            IssuedSegmentSnapshot issuedSegment,
            string? fareBasis,
            decimal issuanceValue,
            int currencyId)
        {
            Id = id;
            TicketId = ticketId;
            CouponNumber = couponNumber;
            OrderServiceId = orderServiceId;
            CurrentOrderServiceId = orderServiceId;
            JourneySegmentId = journeySegmentId;
            IssuedSegment = issuedSegment;
            FareBasisSnapshot = fareBasis;
            IssuanceValue = issuanceValue;
            CurrencyId = currencyId;
            FinancialStatus = TicketCouponFinancialStatus.Open;
            ControlStatus = TicketCouponControlStatus.Local;
        }

        public long TicketId { get; private set; }

        public int CouponNumber { get; private set; }

        public long OrderServiceId { get; private set; }

        public long CurrentOrderServiceId { get; private set; }

        public long JourneySegmentId { get; private set; }

        public IssuedSegmentSnapshot IssuedSegment { get; private set; } = default!;

        public string? FareBasisSnapshot { get; private set; }

        public decimal IssuanceValue { get; private set; }

        public int CurrencyId { get; private set; }

        public TicketCouponFinancialStatus FinancialStatus { get; private set; }

        public TicketCouponControlStatus ControlStatus { get; private set; }

        public string? ProviderCouponStatusCode { get; private set; }

        internal void Void() => FinancialStatus = TicketCouponFinancialStatus.Void;

        internal void RecordProviderStatus(string? providerCouponStatusCode, TicketCouponControlStatus controlStatus)
        {
            ProviderCouponStatusCode = providerCouponStatusCode;
            ControlStatus = controlStatus;
        }
    }
}
