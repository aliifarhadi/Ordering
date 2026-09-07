using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderItinerary : Entity<long>
    {
        private OrderItinerary()
        {
        }

        public OrderItinerary(
            long id,
            long orderId,
            long originAirportId,
            long destinationAirportId,
            string boundId,
            int sequence,
            BoundDirection boundDirection)
        {
            Id = id;
            OrderId = orderId;
            OriginAirportId = originAirportId;
            DestinationAirportId = destinationAirportId;
            BoundId = boundId;
            Sequence = sequence;
            BoundDirection = boundDirection;
        }

        public long OrderId { get; private set; }

        public long OriginAirportId { get; private set; }

        public long DestinationAirportId { get; private set; }

        public string BoundId { get; private set; } = string.Empty;

        public int Sequence { get; private set; }

        public BoundDirection BoundDirection { get; private set; }

        internal OrderItinerary CopyTo(long newId, long newOrderId)
            => new(newId, newOrderId, OriginAirportId, DestinationAirportId, BoundId, Sequence, BoundDirection);
    }
}
