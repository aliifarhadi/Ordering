using AeroTech.Framework.Core.Domain.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects
{
    public sealed class IssuedSegmentSnapshot : ValueObject
    {
        private IssuedSegmentSnapshot()
        {
        }

        public IssuedSegmentSnapshot(
            int marketingAirlineId,
            string flightNumber,
            int originAirportId,
            int destinationAirportId,
            DateTimeOffset departureDateTime,
            DateTimeOffset arrivalDateTime,
            string? bookingClass)
        {
            MarketingAirlineId = marketingAirlineId;
            FlightNumber = flightNumber;
            OriginAirportId = originAirportId;
            DestinationAirportId = destinationAirportId;
            DepartureDateTime = departureDateTime;
            ArrivalDateTime = arrivalDateTime;
            BookingClass = bookingClass;
        }

        public int MarketingAirlineId { get; private set; }

        public string FlightNumber { get; private set; } = default!;

        public int OriginAirportId { get; private set; }

        public int DestinationAirportId { get; private set; }

        public DateTimeOffset DepartureDateTime { get; private set; }

        public DateTimeOffset ArrivalDateTime { get; private set; }

        public string? BookingClass { get; private set; }

        public TicketedSegmentSnapshot AsTicketedSegment()
            => new(
                MarketingAirlineId,
                FlightNumber,
                OriginAirportId,
                DestinationAirportId,
                DepartureDateTime,
                ArrivalDateTime,
                BookingClass);

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return MarketingAirlineId;
            yield return FlightNumber;
            yield return OriginAirportId;
            yield return DestinationAirportId;
            yield return DepartureDateTime;
            yield return ArrivalDateTime;
            yield return BookingClass;
        }
    }
}
