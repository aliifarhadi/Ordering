using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderSegment : Entity<long>
    {
        private readonly List<OrderSegmentLeg> _legs = new();

        private OrderSegment()
        {
        }

        public OrderSegment(long orderId, CreateOrderSegmentArgs args)
        {
            Id = args.Id;
            OrderId = orderId;
            OrderItineraryId = args.OrderItineraryId;
            Sequence = args.Sequence;
            CabinClassId = args.CabinClassId;
            RbdId = args.RbdId;
            BookingClassCode = args.BookingClassCode;
            FlightCapacityId = args.FlightCapacityId;
            BookingClass = args.BookingClass;
            AirFareId = args.AirFareId;
            FlightId = args.FlightId;
            FlightVersion = args.FlightVersion;
            Number = args.Number;
            OriginAirportId = args.OriginAirportId;
            OriginAirportTerminalId = args.OriginAirportTerminalId;
            DestinationAirportId = args.DestinationAirportId;
            DestinationAirportTerminalId = args.DestinationAirportTerminalId;
            OperatingAirlineId = args.OperatingAirlineId;
            MarketingAirlineId = args.MarketingAirlineId;
            DepartureDateTime = args.DepartureDateTime;
            ArrivalDateTime = args.ArrivalDateTime;
            Duration = args.Duration;
            AircraftId = args.AircraftId;
        }

        public long OrderId { get; private set; }

        public long OrderItineraryId { get; private set; }

        public int Sequence { get; private set; }

        public int? CabinClassId { get; private set; }

        public long? RbdId { get; private set; }

        public string? BookingClassCode { get; private set; }

        public long FlightCapacityId { get; private set; }

        public string? BookingClass { get; private set; }

        public long AirFareId { get; private set; }

        public long FlightId { get; private set; }

        public int FlightVersion { get; private set; }

        public string FlightVersionId => $"{FlightId}_{FlightVersion}";

        public string Number { get; private set; } = default!;

        public int OriginAirportId { get; private set; }

        public int? OriginAirportTerminalId { get; private set; }

        public int DestinationAirportId { get; private set; }

        public int? DestinationAirportTerminalId { get; private set; }

        public int OperatingAirlineId { get; private set; }

        public int MarketingAirlineId { get; private set; }

        public DateTimeOffset DepartureDateTime { get; private set; }

        public DateTimeOffset ArrivalDateTime { get; private set; }

        public int Duration { get; private set; }

        public int AircraftId { get; private set; }

        public IReadOnlyCollection<OrderSegmentLeg> Legs => _legs.AsReadOnly();

        public OrderSegmentLeg AddLeg(CreateOrderSegmentLegArgs args)
        {
            var leg = new OrderSegmentLeg(Id, args);
            _legs.Add(leg);
            return leg;
        }

        internal OrderSegment CopyTo(long newId, long newOrderId, long newItineraryId, IIdGenerator idGenerator)
        {
            var copy = new OrderSegment(newOrderId, new CreateOrderSegmentArgs(
                newId, newItineraryId, Sequence, CabinClassId, RbdId, BookingClassCode, FlightCapacityId,
                BookingClass, AirFareId, FlightId, FlightVersion, Number, OriginAirportId, OriginAirportTerminalId,
                DestinationAirportId, DestinationAirportTerminalId, OperatingAirlineId, MarketingAirlineId,
                DepartureDateTime, ArrivalDateTime, Duration, AircraftId));

            foreach (var leg in _legs)
                copy.AddLeg(new CreateOrderSegmentLegArgs(
                    idGenerator.NewId(), leg.Sequence, leg.LegId, leg.OriginAirportId, leg.OriginAirportTerminalId,
                    leg.DestinationAirportId, leg.DestinationAirportTerminalId, leg.DepartureDateTime, leg.ArrivalDateTime,
                    leg.StopType, leg.StopDurationAtArrivalAirport));

            return copy;
        }
    }
}
