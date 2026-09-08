using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderLoungeServiceDetail : Entity<long>
    {
        private OrderLoungeServiceDetail()
        {
        }

        internal OrderLoungeServiceDetail(
            long id,
            long orderServiceId,
            int airportId,
            string? loungeCode,
            DateTimeOffset? accessStart,
            DateTimeOffset? accessEnd,
            int guestCount,
            long? relatedAirOrderServiceId)
        {
            if (airportId <= 0)
                throw ExceptionFactory.LoungeRequiresAirport();

            if (guestCount < 0)
                throw ExceptionFactory.LoungeGuestCountMustBeNonNegative();

            if (accessStart.HasValue && accessEnd.HasValue && accessEnd <= accessStart)
                throw ExceptionFactory.LoungeAccessWindowInvalid();

            Id = id;
            OrderServiceId = orderServiceId;
            AirportId = airportId;
            LoungeCode = loungeCode;
            AccessStart = accessStart;
            AccessEnd = accessEnd;
            GuestCount = guestCount;
            RelatedAirOrderServiceId = relatedAirOrderServiceId;
        }

        public long OrderServiceId { get; private set; }

        public int AirportId { get; private set; }

        public string? LoungeCode { get; private set; }

        public DateTimeOffset? AccessStart { get; private set; }

        public DateTimeOffset? AccessEnd { get; private set; }

        public int GuestCount { get; private set; }

        public long? RelatedAirOrderServiceId { get; private set; }
    }
}
