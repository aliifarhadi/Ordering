using System.Reflection;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class ServiceCompositionTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void The_common_order_service_is_a_concrete_entity()
        {
            Assert.False(typeof(OrderService).IsAbstract);
            Assert.True(typeof(OrderService).IsSealed);
        }

        [Fact]
        public void No_domain_type_derives_from_the_common_order_service()
        {
            var derived = typeof(Order).Assembly.GetTypes()
                .Where(type => type != typeof(OrderService) && typeof(OrderService).IsAssignableFrom(type))
                .ToList();

            Assert.Empty(derived);
        }

        [Fact]
        public void Air_transportation_is_the_common_service_plus_an_air_detail()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var air = order.AirTransportServices.ToList();

            Assert.Equal(4, air.Count);
            Assert.All(air, service =>
            {
                Assert.IsType<OrderService>(service);
                Assert.NotNull(service.AirTransportDetail);
                Assert.Equal(OrderServiceType.AirTransportation, service.ServiceType);
            });
        }

        [Fact]
        public void The_sold_segment_is_reached_through_the_air_detail_only()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var segmentIds = order.Segments.Select(segment => segment.Id).ToHashSet();

            Assert.All(order.AirTransportServices, service =>
            {
                Assert.NotNull(service.SoldSegmentId);
                Assert.Contains(service.SoldSegmentId!.Value, segmentIds);
                Assert.Equal(service.AirTransportDetail!.OrderSegmentId, service.SoldSegmentId);
            });

            Assert.DoesNotContain(
                typeof(OrderService).GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.Name == "OrderSegmentId");
        }

        [Fact]
        public void A_non_air_service_has_no_sold_segment()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Meal());

            var meal = Single(order, OrderServiceType.Meal);

            Assert.Null(meal.SoldSegmentId);
            Assert.Null(meal.AirTransportDetail);
        }

        [Fact]
        public void Every_created_service_carries_exactly_one_typed_detail()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Seat(),
                AncillaryFactory.Baggage(),
                AncillaryFactory.Meal(),
                AncillaryFactory.Lounge(),
                AncillaryFactory.Hotel(),
                AncillaryFactory.GroundTransport(),
                AncillaryFactory.Priority());

            Assert.Equal(11, order.OrderServices.Count);
            Assert.All(order.OrderServices, service => Assert.Equal(1, service.AttachedDetailCount));
        }

        [Fact]
        public void A_detail_that_contradicts_the_service_type_is_rejected()
        {
            var mismatched = AncillaryFactory.Service(
                "BAD-1",
                OrderServiceType.Meal,
                "MEAL",
                new AcceptedSeatDetail(AncillaryFactory.OutboundAirServiceRef()),
                ["T1"]);

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, mismatched));

            Assert.Equal(2824, exception.Code);
        }

        [Fact]
        public void An_air_detail_on_an_ancillary_service_is_rejected()
        {
            var mismatched = AncillaryFactory.Service(
                "BAD-2",
                OrderServiceType.LoungeAccess,
                "LNG",
                new AcceptedAirTransportDetail(AncillaryFactory.OutboundSegmentRef()),
                ["T1"]);

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, mismatched));

            Assert.Equal(2824, exception.Code);
        }

        [Fact]
        public void Each_service_has_its_own_identity()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat(), AncillaryFactory.Baggage());

            var ids = order.OrderServices.Select(service => service.Id).ToList();

            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.DoesNotContain(0L, ids);
        }

        [Fact]
        public void The_service_owns_its_fulfillment_profile()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Meal());

            var air = order.AirTransportServices.First();
            var meal = Single(order, OrderServiceType.Meal);

            Assert.True(air.RequiresReservation);
            Assert.True(air.RequiresDocument);
            Assert.Equal(ServiceDocumentKind.ElectronicTicket, air.DocumentKind);

            Assert.False(meal.RequiresReservation);
            Assert.False(meal.RequiresDocument);
            Assert.Null(meal.DocumentKind);
        }

        [Fact]
        public void Price_treatment_is_recorded_and_is_not_a_financial_status()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Meal("MEAL-INCLUDED", priceTreatment: ServicePriceTreatment.Included));

            var meal = Single(order, OrderServiceType.Meal);

            Assert.Equal(ServicePriceTreatment.Included, meal.PriceTreatment);
            Assert.Equal(OrderServiceFinancialStatus.Priced, meal.FinancialStatus);
        }

        [Fact]
        public void An_included_service_is_still_a_first_class_service()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Baggage("BAG-INCLUDED", BaggageServiceKind.Allowance, pieces: 1,
                    priceTreatment: ServicePriceTreatment.Included));

            var baggage = Single(order, OrderServiceType.BaggageAllowance);

            Assert.Equal(ServicePriceTreatment.Included, baggage.PriceTreatment);
            Assert.NotNull(baggage.BaggageDetail);
            Assert.Single(baggage.Beneficiaries);
            Assert.Equal(OrderServiceStatus.Active, baggage.Status);
            Assert.DoesNotContain(
                order.PricingLines,
                line => line.BasisType == PricingBasisType.OrderService && line.BasisReferenceId == baggage.Id);
        }

        [Fact]
        public void A_complimentary_service_is_distinguished_from_an_included_one()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Meal("MEAL-COMP", priceTreatment: ServicePriceTreatment.Complimentary),
                AncillaryFactory.Baggage("BAG-INC", priceTreatment: ServicePriceTreatment.Included));

            Assert.Equal(ServicePriceTreatment.Complimentary, Single(order, OrderServiceType.Meal).PriceTreatment);
            Assert.Equal(ServicePriceTreatment.Included, Single(order, OrderServiceType.BaggageAllowance).PriceTreatment);
        }

        [Fact]
        public void A_document_kind_is_recorded_only_when_a_document_is_required()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Priority(), AncillaryFactory.ExtraSeat());

            var priority = Single(order, OrderServiceType.Priority);
            var extraSeat = Single(order, OrderServiceType.ExtraSeat);

            Assert.False(priority.RequiresDocument);
            Assert.Null(priority.DocumentKind);

            Assert.True(extraSeat.RequiresDocument);
            Assert.Equal(ServiceDocumentKind.ElectronicMiscDocument, extraSeat.DocumentKind);
        }

        [Fact]
        public void A_service_belongs_to_its_current_item_and_keeps_its_original_membership()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Meal());

            var meal = Single(order, OrderServiceType.Meal);
            var membership = order.OriginalItemMembership(meal.Id).ToList();

            Assert.Single(membership);
            Assert.Equal(meal.OrderItemId, membership[0].OrderItemId);
            Assert.NotEqual(0L, membership[0].LinkedByChangeId);
        }

        [Fact]
        public void Every_created_service_is_linked_to_the_item_that_created_it()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat(), AncillaryFactory.Baggage());

            Assert.Equal(order.OrderServices.Count, order.ItemServiceLinks.Count);
            Assert.All(order.ItemServiceLinks, link =>
            {
                Assert.Equal(order.Id, link.OrderId);
                Assert.Contains(order.Items, item => item.Id == link.OrderItemId);
                Assert.Contains(order.OrderServices, service => service.Id == link.OrderServiceId);
            });
        }

        [Fact]
        public void Item_membership_links_record_the_change_that_created_them()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Meal());

            var changeIds = order.Changes.Select(change => change.Id).ToHashSet();

            Assert.All(order.ItemServiceLinks, link => Assert.Contains(link.LinkedByChangeId, changeIds));
        }

        private static OrderService Single(Order order, OrderServiceType serviceType)
            => order.OrderServices.Single(service => service.ServiceType == serviceType);
    }
}
