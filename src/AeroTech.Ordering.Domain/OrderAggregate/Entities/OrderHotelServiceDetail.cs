using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderHotelServiceDetail : Entity<long>
    {
        private OrderHotelServiceDetail()
        {
        }

        internal OrderHotelServiceDetail(
            long id,
            long orderServiceId,
            string propertyReference,
            DateOnly checkIn,
            DateOnly checkOut,
            int roomCount,
            int guestCount,
            string? supplierReference,
            string? roomTypeCode,
            string? ratePlanReference)
        {
            if (checkOut <= checkIn)
                throw ExceptionFactory.HotelStayWindowInvalid();

            if (roomCount <= 0)
                throw ExceptionFactory.HotelRoomCountMustBePositive();

            if (guestCount <= 0)
                throw ExceptionFactory.HotelGuestCountMustBePositive();

            Id = id;
            OrderServiceId = orderServiceId;
            PropertyReference = propertyReference;
            CheckIn = checkIn;
            CheckOut = checkOut;
            RoomCount = roomCount;
            GuestCount = guestCount;
            SupplierReference = supplierReference;
            RoomTypeCode = roomTypeCode;
            RatePlanReference = ratePlanReference;
        }

        public long OrderServiceId { get; private set; }

        public string PropertyReference { get; private set; } = default!;

        public DateOnly CheckIn { get; private set; }

        public DateOnly CheckOut { get; private set; }

        public int RoomCount { get; private set; }

        public int GuestCount { get; private set; }

        public string? SupplierReference { get; private set; }

        public string? RoomTypeCode { get; private set; }

        public string? RatePlanReference { get; private set; }
    }
}
