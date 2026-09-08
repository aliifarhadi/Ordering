using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderAirTransportServiceDetail : Entity<long>
    {
        private OrderAirTransportServiceDetail()
        {
        }

        internal OrderAirTransportServiceDetail(
            long id,
            long orderServiceId,
            long orderSegmentId,
            string? transitionalFareBasis,
            string? requestedSeat,
            Baggage? transitionalCheckedBaggage = null,
            Baggage? transitionalCabinBaggage = null)
        {
            Id = id;
            OrderServiceId = orderServiceId;
            OrderSegmentId = orderSegmentId;
            TransitionalFareBasis = transitionalFareBasis;
            RequestedSeat = requestedSeat;
            TransitionalCheckedBaggage = transitionalCheckedBaggage;
            TransitionalCabinBaggage = transitionalCabinBaggage;
        }

        public long OrderServiceId { get; private set; }

        public long OrderSegmentId { get; private set; }

        public string? TransitionalFareBasis { get; private set; }

        public string? RequestedSeat { get; private set; }

        public Baggage? TransitionalCheckedBaggage { get; private set; }

        public Baggage? TransitionalCabinBaggage { get; private set; }
    }
}
