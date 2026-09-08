using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class SeatServiceTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_seat_service_is_the_common_service_plus_a_seat_detail()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat());

            var seat = Seat(order);

            Assert.Equal(OrderServiceType.SeatAssignment, seat.ServiceType);
            Assert.NotNull(seat.SeatDetail);
            Assert.Equal(1, seat.AttachedDetailCount);
        }

        [Fact]
        public void A_seat_service_points_at_the_air_service_it_belongs_to()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat());

            var seat = Seat(order);
            var air = order.AirTransportServices.Single(service => service.Id == seat.SeatDetail!.AssociatedAirOrderServiceId);

            Assert.Equal(air.SoleBeneficiaryId, seat.SoleBeneficiaryId);
        }

        [Fact]
        public void The_sold_seat_number_is_recorded_when_the_source_states_one()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat(seatNumber: "12A"));

            Assert.Equal("12A", Seat(order).SeatDetail!.SoldSeatNumber);
        }

        [Fact]
        public void A_seat_service_without_a_sold_seat_number_is_still_valid()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat(seatNumber: null));

            Assert.Null(Seat(order).SeatDetail!.SoldSeatNumber);
        }

        [Fact]
        public void A_seat_service_referencing_an_unknown_air_service_is_rejected()
        {
            var broken = AncillaryFactory.Service(
                "SEAT-BAD",
                OrderServiceType.SeatAssignment,
                "SEAT",
                new AcceptedSeatDetail("NOT-A-SERVICE"),
                ["T1"]);

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, broken));

            Assert.Equal(2784, exception.Code);
        }

        [Fact]
        public void A_seat_service_referencing_a_non_air_service_is_rejected()
        {
            var meal = AncillaryFactory.Meal("MEAL-FIRST");

            var broken = AncillaryFactory.Service(
                "SEAT-BAD-2",
                OrderServiceType.SeatAssignment,
                "SEAT",
                new AcceptedSeatDetail("MEAL-FIRST"),
                ["T1"]);

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, meal, broken));

            Assert.Equal(2784, exception.Code);
        }

        [Fact]
        public void A_seat_service_is_not_an_air_service()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat());

            var seat = Seat(order);

            Assert.False(seat.IsAirTransport);
            Assert.Null(seat.SoldSegmentId);
            Assert.DoesNotContain(order.AirTransportServices, service => service.Id == seat.Id);
        }

        private static OrderService Seat(Order order)
            => order.OrderServices.Single(service => service.ServiceType == OrderServiceType.SeatAssignment);
    }
}
