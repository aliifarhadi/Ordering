using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.FlightFlow.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderSegmentLeg : Entity<long>
    {
        private OrderSegmentLeg()
        {
        }

        public OrderSegmentLeg(long orderSegmentId, CreateOrderSegmentLegArgs args)
        {
            Id = args.Id;
            OrderSegmentId = orderSegmentId;
            Sequence = args.Sequence;
            LegId = args.LegId;
            OriginAirportId = args.OriginAirportId;
            OriginAirportTerminalId = args.OriginAirportTerminalId;
            DestinationAirportId = args.DestinationAirportId;
            DestinationAirportTerminalId = args.DestinationAirportTerminalId;
            DepartureDateTime = args.DepartureDateTime;
            ArrivalDateTime = args.ArrivalDateTime;
            StopType = args.StopType;
            StopDurationAtArrivalAirport = args.StopDurationAtArrivalAirport;
        }

        public long OrderSegmentId { get; private set; }

        public int Sequence { get; private set; }

        public long LegId { get; private set; }

        public int OriginAirportId { get; private set; }

        public int? OriginAirportTerminalId { get; private set; }

        public int DestinationAirportId { get; private set; }

        public int? DestinationAirportTerminalId { get; private set; }

        public DateTimeOffset DepartureDateTime { get; private set; }

        public DateTimeOffset ArrivalDateTime { get; private set; }

        public FlightStopType? StopType { get; private set; }

        public int? StopDurationAtArrivalAirport { get; private set; }
    }
}
