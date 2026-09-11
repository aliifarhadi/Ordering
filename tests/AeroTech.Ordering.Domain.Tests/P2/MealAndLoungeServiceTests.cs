using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class MealAndLoungeServiceTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_meal_service_records_its_code_and_quantity()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Meal(quantity: 2));

            var detail = Of(order, OrderServiceType.Meal).MealDetail!;

            Assert.Equal("VGML", detail.MealCode);
            Assert.Equal(2, detail.Quantity);
            Assert.Equal("VG", detail.SpecialMealCode);
        }

        [Fact]
        public void A_meal_quantity_must_be_positive()
        {
            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Meal(quantity: 0)));

            Assert.Equal(20145, exception.Code);
        }

        [Fact]
        public void A_meal_service_states_the_air_service_it_is_served_on()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Meal());

            var covered = Assert.Single(Of(order, OrderServiceType.Meal).CoveredServices);

            Assert.Contains(order.AirTransportServices, air => air.Id == covered.CoveredOrderServiceId);
        }

        [Fact]
        public void An_included_meal_is_still_a_service()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Meal(priceTreatment: ServicePriceTreatment.Included));

            var meal = Of(order, OrderServiceType.Meal);

            Assert.Equal(ServicePriceTreatment.Included, meal.PriceTreatment);
            Assert.NotNull(meal.MealDetail);
        }

        [Fact]
        public void A_lounge_service_records_the_airport_it_applies_to()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Lounge(airportId: 200));

            var detail = Of(order, OrderServiceType.LoungeAccess).LoungeDetail!;

            Assert.Equal(200, detail.AirportId);
            Assert.Equal("LNG-A", detail.LoungeCode);
        }

        [Fact]
        public void A_lounge_service_without_an_airport_is_rejected()
        {
            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Lounge(airportId: 0)));

            Assert.Equal(20146, exception.Code);
        }

        [Fact]
        public void A_lounge_guest_count_must_not_be_negative()
        {
            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Lounge(guestCount: -1)));

            Assert.Equal(20147, exception.Code);
        }

        [Fact]
        public void A_lounge_access_window_must_move_forward_in_time()
        {
            var start = _clock.GetDateTime().AddDays(30);

            var exception = Assert.Throws<BusinessException>(() => AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Lounge(accessStart: start, accessEnd: start.AddHours(-1))));

            Assert.Equal(20148, exception.Code);
        }

        [Fact]
        public void A_lounge_service_may_be_bound_to_an_air_service()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Lounge(relatedAirServiceRef: AncillaryFactory.OutboundAirServiceRef()));

            var detail = Of(order, OrderServiceType.LoungeAccess).LoungeDetail!;

            Assert.NotNull(detail.RelatedAirOrderServiceId);
            Assert.Contains(order.AirTransportServices, air => air.Id == detail.RelatedAirOrderServiceId);
        }

        [Fact]
        public void A_lounge_service_that_is_not_bound_to_a_flight_is_still_valid()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Lounge());

            var lounge = Of(order, OrderServiceType.LoungeAccess);

            Assert.Null(lounge.LoungeDetail!.RelatedAirOrderServiceId);
            Assert.Empty(lounge.CoveredServices);
        }

        [Fact]
        public void A_lounge_detail_on_a_meal_service_is_rejected()
        {
            var mismatched = AncillaryFactory.Service(
                "LNG-BAD",
                OrderServiceType.Meal,
                "MEAL",
                new AcceptedLoungeDetail(100, 0),
                ["T1"]);

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, mismatched));

            Assert.Equal(20138, exception.Code);
        }

        private static OrderService Of(Order order, OrderServiceType serviceType)
            => order.OrderServices.Single(service => service.ServiceType == serviceType);
    }
}
