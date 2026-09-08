using AeroTech.Framework.Core.Domain.Entities;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderSeatServiceDetail : Entity<long>
    {
        private OrderSeatServiceDetail()
        {
        }

        internal OrderSeatServiceDetail(
            long id,
            long orderServiceId,
            long associatedAirOrderServiceId,
            string? soldSeatNumber)
        {
            Id = id;
            OrderServiceId = orderServiceId;
            AssociatedAirOrderServiceId = associatedAirOrderServiceId;
            SoldSeatNumber = soldSeatNumber;
        }

        public long OrderServiceId { get; private set; }

        public long AssociatedAirOrderServiceId { get; private set; }

        public string? SoldSeatNumber { get; private set; }
    }
}
