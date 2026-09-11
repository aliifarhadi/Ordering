using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class HotelAndGroundTransportServiceTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_hotel_service_records_its_stay_window()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel(nights: 3));

            var detail = Of(order, OrderServiceType.HotelStay).HotelDetail!;

            Assert.Equal(new DateOnly(2026, 10, 1), detail.CheckIn);
            Assert.Equal(new DateOnly(2026, 10, 4), detail.CheckOut);
            Assert.Equal("PROP-1", detail.PropertyReference);
        }

        [Fact]
        public void A_hotel_stay_that_does_not_advance_is_rejected()
        {
            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel(nights: 0)));

            Assert.Equal(20149, exception.Code);
        }

        [Fact]
        public void A_hotel_service_needs_at_least_one_room()
        {
            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel(roomCount: 0)));

            Assert.Equal(20150, exception.Code);
        }

        [Fact]
        public void A_hotel_service_needs_at_least_one_guest()
        {
            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel(guestCount: 0)));

            Assert.Equal(20151, exception.Code);
        }

        [Fact]
        public void A_hotel_service_is_shared_by_its_beneficiaries()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel());

            var hotel = Of(order, OrderServiceType.HotelStay);

            Assert.Equal(2, hotel.Beneficiaries.Count);
            Assert.Equal(2, hotel.HotelDetail!.GuestCount);
        }

        [Fact]
        public void A_hotel_service_is_not_bound_to_a_segment()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel());

            var hotel = Of(order, OrderServiceType.HotelStay);

            Assert.Null(hotel.SoldSegmentId);
            Assert.Empty(hotel.CoveredSegments);
            Assert.Empty(hotel.CoveredServices);
        }

        [Fact]
        public void A_hotel_service_keeps_the_supplier_references_the_source_gave()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel());

            var detail = Of(order, OrderServiceType.HotelStay).HotelDetail!;

            Assert.Equal("SUP-1", detail.SupplierReference);
            Assert.Equal("DBL", detail.RoomTypeCode);
            Assert.Equal("RATE-1", detail.RatePlanReference);
        }

        [Fact]
        public void A_ground_transport_service_records_its_pickup()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.GroundTransport());

            var detail = Of(order, OrderServiceType.GroundTransport).GroundTransportDetail!;

            Assert.Equal("LOC-A", detail.PickupLocationReference);
            Assert.Equal("LOC-B", detail.DropoffLocationReference);
            Assert.Equal(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), detail.PickupAt);
            Assert.Equal("VAN", detail.VehicleTypeCode);
        }

        [Fact]
        public void A_ground_transport_service_needs_at_least_one_passenger()
        {
            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.GroundTransport(passengerCount: 0)));

            Assert.Equal(20152, exception.Code);
        }

        [Fact]
        public void A_ground_transport_service_can_serve_several_travellers()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.GroundTransport());

            var ground = Of(order, OrderServiceType.GroundTransport);

            Assert.Equal(2, ground.Beneficiaries.Count);
            Assert.Equal(3, ground.GroundTransportDetail!.PassengerCount);
        }

        [Fact]
        public void Ground_transport_is_a_first_class_type_and_not_a_transfer_ride()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.GroundTransport());

            Assert.Contains(order.OrderServices, service => service.ServiceType == OrderServiceType.GroundTransport);
            Assert.DoesNotContain(order.OrderServices, service => service.ServiceType == OrderServiceType.TransferRide);
        }

        [Fact]
        public void A_hotel_detail_on_a_ground_transport_service_is_rejected()
        {
            var mismatched = AncillaryFactory.Service(
                "GRD-BAD",
                OrderServiceType.GroundTransport,
                "GRD",
                new AcceptedHotelDetail("P", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), 1, 1),
                ["T1"]);

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, mismatched));

            Assert.Equal(20138, exception.Code);
        }

        private static OrderService Of(Order order, OrderServiceType serviceType)
            => order.OrderServices.Single(service => service.ServiceType == serviceType);
    }
}
