using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderGroundTransportServiceDetail : Entity<long>
    {
        private OrderGroundTransportServiceDetail()
        {
        }

        internal OrderGroundTransportServiceDetail(
            long id,
            long orderServiceId,
            string pickupLocationReference,
            string dropoffLocationReference,
            DateTimeOffset pickupAt,
            int passengerCount,
            string? vehicleTypeCode)
        {
            if (passengerCount <= 0)
                throw ExceptionFactory.GroundTransportPassengerCountMustBePositive();

            Id = id;
            OrderServiceId = orderServiceId;
            PickupLocationReference = pickupLocationReference;
            DropoffLocationReference = dropoffLocationReference;
            PickupAt = pickupAt;
            PassengerCount = passengerCount;
            VehicleTypeCode = vehicleTypeCode;
        }

        public long OrderServiceId { get; private set; }

        public string PickupLocationReference { get; private set; } = default!;

        public string DropoffLocationReference { get; private set; } = default!;

        public DateTimeOffset PickupAt { get; private set; }

        public int PassengerCount { get; private set; }

        public string? VehicleTypeCode { get; private set; }
    }
}
