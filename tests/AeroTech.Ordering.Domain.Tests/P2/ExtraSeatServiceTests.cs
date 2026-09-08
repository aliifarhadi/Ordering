using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class ExtraSeatServiceTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void An_extra_seat_is_a_service_and_never_a_traveller()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.ExtraSeat());

            Assert.Equal(2, order.Travellers.Count);
            Assert.Contains(order.OrderServices, service => service.ServiceType == OrderServiceType.ExtraSeat);
        }

        [Fact]
        public void An_extra_seat_requires_a_reservation()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.ExtraSeat());

            Assert.True(ExtraSeat(order).RequiresReservation);
        }

        [Fact]
        public void An_extra_seat_is_documented_by_an_electronic_miscellaneous_document()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.ExtraSeat());

            var extraSeat = ExtraSeat(order);

            Assert.True(extraSeat.RequiresDocument);
            Assert.Equal(ServiceDocumentKind.ElectronicMiscDocument, extraSeat.DocumentKind);
        }

        [Fact]
        public void An_extra_seat_states_its_capacity_and_reason()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.ExtraSeat());

            Assert.Equal("""{"capacityQuantity":1,"reason":"CBBG"}""", ExtraSeat(order).GenericDetail!.AttributesJson);
        }

        [Fact]
        public void An_extra_seat_without_a_capacity_quantity_is_rejected()
        {
            var incomplete = AncillaryFactory.Generic(
                "EXST-BAD",
                OrderServiceType.ExtraSeat,
                "ExtraSeat",
                "1.0",
                """{"reason":"CBBG"}""");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, incomplete));

            Assert.Equal(2828, exception.Code);
        }

        [Fact]
        public void An_extra_seat_without_a_reason_is_rejected()
        {
            var incomplete = AncillaryFactory.Generic(
                "EXST-BAD-2",
                OrderServiceType.ExtraSeat,
                "ExtraSeat",
                "1.0",
                """{"capacityQuantity":1}""");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, incomplete));

            Assert.Equal(2828, exception.Code);
        }

        [Fact]
        public void An_extra_seat_states_the_air_service_it_occupies_capacity_on()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.ExtraSeat());

            var covered = Assert.Single(ExtraSeat(order).CoveredServices);

            Assert.Contains(order.AirTransportServices, air => air.Id == covered.CoveredOrderServiceId);
        }

        [Fact]
        public void An_extra_seat_is_attributed_to_the_traveller_who_bought_it()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.ExtraSeat());

            var extraSeat = ExtraSeat(order);

            Assert.Single(extraSeat.Beneficiaries);
            Assert.Contains(order.Travellers, traveller => traveller.Id == extraSeat.SoleBeneficiaryId);
        }

        [Fact]
        public void An_extra_seat_is_not_a_seat_assignment()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.ExtraSeat());

            var extraSeat = ExtraSeat(order);

            Assert.Null(extraSeat.SeatDetail);
            Assert.DoesNotContain(order.OrderServices, service => service.ServiceType == OrderServiceType.SeatAssignment);
        }

        private static OrderService ExtraSeat(Order order)
            => order.OrderServices.Single(service => service.ServiceType == OrderServiceType.ExtraSeat);
    }
}
