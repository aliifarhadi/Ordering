using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class AddProductReferenceTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_service_without_a_beneficiary_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(WithService(order, service => service with { BeneficiaryTravellerIds = [] }), _ids, _clock));

            Assert.Equal(2821, exception.Code);
        }

        [Fact]
        public void An_unknown_beneficiary_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(WithService(order, service => service with { BeneficiaryTravellerIds = [-1L] }), _ids, _clock));

            Assert.Equal(2858, exception.Code);
        }

        [Fact]
        public void A_beneficiary_from_another_order_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var other = MultiPassengerOrderFactory.Create(_ids, _clock);
            var foreignTravellerId = other.Travellers.First().Id;

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(
                    WithService(order, service => service with { BeneficiaryTravellerIds = [foreignTravellerId] }),
                    _ids,
                    _clock));

            Assert.Equal(2858, exception.Code);
        }

        [Fact]
        public void An_air_service_from_another_order_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var other = MultiPassengerOrderFactory.Create(_ids, _clock);
            var foreignAir = ProductAdditionFactory.OutboundAirService(other);

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(
                    WithService(order, service => service with { Detail = new AcceptedAddedSeatDetail(foreignAir.Id, "1A") }),
                    _ids,
                    _clock));

            Assert.Equal(2858, exception.Code);
        }

        [Fact]
        public void A_cancelled_target_air_service_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var air = ProductAdditionFactory.OutboundAirService(order);
            var addition = ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order));

            order.WithdrawBeforeTicketing([air.Id], VoidReason.CustomerRequest, 7, _ids, _clock);

            var exception = Assert.Throws<BusinessException>(() => order.AddProduct(addition, _ids, _clock));

            Assert.Equal(2859, exception.Code);
        }

        [Fact]
        public void A_seat_target_must_be_an_air_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.GroundTransport(order)), _ids, _clock);

            var ground = order.OrderServices.Single(service => service.ServiceType == OrderServiceType.GroundTransport);

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(
                    WithService(
                        order,
                        service => service with { Detail = new AcceptedAddedSeatDetail(ground.Id, "1A") },
                        ProductAdditionFactory.OperationId + 1),
                    _ids,
                    _clock));

            Assert.Equal(2858, exception.Code);
        }

        [Fact]
        public void A_covered_service_must_be_an_air_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var travellerId = order.Travellers.First().Id;

            var meal = ProductAdditionFactory.Service(
                "MEAL-1",
                OrderServiceType.Meal,
                "MEAL",
                new AcceptedMealDetail("VGML", 1),
                [travellerId],
                ServicePriceTreatment.SupplierOpaque,
                coveredOrderServiceIds: [-5L]);

            var addition = ProductAdditionFactory.Addition(
                ProductAdditionFactory.Product(ProductType.Meal, [meal]),
                [ProductAdditionFactory.Line(PricingComponentType.ProductCharge, 1_000m, PricingBasisType.OrderItem)]);

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(addition), _ids, _clock));

            Assert.Equal(2858, exception.Code);
        }

        [Fact]
        public void Baggage_may_cover_several_existing_air_services()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var outbound = ProductAdditionFactory.OutboundAirService(order);
            var inbound = ProductAdditionFactory.InboundAirService(order);

            var baggage = ProductAdditionFactory.Service(
                "BAG-RT",
                OrderServiceType.BaggageAllowance,
                "BAG",
                new AcceptedBaggageDetail(BaggageServiceKind.PrepaidPiece, Pieces: 2),
                [outbound.SoleBeneficiaryId],
                ServicePriceTreatment.Included,
                coveredOrderServiceIds: [outbound.Id, inbound.Id]);

            var addition = ProductAdditionFactory.Addition(
                ProductAdditionFactory.Product(ProductType.Baggage, [baggage]),
                [ProductAdditionFactory.Line(PricingComponentType.ProductCharge, 500_000m, PricingBasisType.OrderItem)]);

            var added = order.AddProduct(ProductAdditionFactory.Args(addition), _ids, _clock);

            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Equal(2, service.CoveredServices.Count);
        }

        [Fact]
        public void A_segment_scope_may_be_declared_on_an_added_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var segmentId = order.Segments.First().Id;
            var travellerId = order.Travellers.First().Id;

            var baggage = ProductAdditionFactory.Service(
                "BAG-SEG",
                OrderServiceType.BaggageAllowance,
                "BAG",
                new AcceptedBaggageDetail(BaggageServiceKind.PrepaidPiece, Pieces: 1),
                [travellerId],
                ServicePriceTreatment.Included,
                coveredOrderSegmentIds: [segmentId]);

            var addition = ProductAdditionFactory.Addition(
                ProductAdditionFactory.Product(ProductType.Baggage, [baggage]),
                [ProductAdditionFactory.Line(PricingComponentType.ProductCharge, 100_000m, PricingBasisType.OrderItem)]);

            var added = order.AddProduct(ProductAdditionFactory.Args(addition), _ids, _clock);

            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Equal(segmentId, Assert.Single(service.CoveredSegments).OrderSegmentId);
            Assert.Empty(service.CoveredServices);
        }

        [Fact]
        public void An_unknown_covered_segment_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(
                    WithService(order, service => service with { CoveredOrderSegmentIds = [-9L] }),
                    _ids,
                    _clock));

            Assert.Equal(2858, exception.Code);
        }

        [Fact]
        public void A_shared_ground_transport_keeps_several_beneficiaries_on_one_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.GroundTransport(order)), _ids, _clock);

            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Single(added.OrderServiceIds);
            Assert.Equal(2, service.Beneficiaries.Count);
            Assert.Equal(2, service.GroundTransportDetail!.PassengerCount);
        }

        [Fact]
        public void A_shared_hotel_keeps_several_beneficiaries_on_one_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Hotel(order)), _ids, _clock);

            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Single(added.OrderServiceIds);
            Assert.Equal(2, service.Beneficiaries.Count);
            Assert.Equal(new DateOnly(2026, 10, 4), service.HotelDetail!.CheckOut);
        }

        [Fact]
        public void An_extra_seat_uses_a_real_traveller_and_a_real_air_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var air = ProductAdditionFactory.OutboundAirService(order);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.ExtraSeat(order)), _ids, _clock);

            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Equal(air.SoleBeneficiaryId, service.SoleBeneficiaryId);
            Assert.Equal(air.Id, Assert.Single(service.CoveredServices).CoveredOrderServiceId);
            Assert.Equal(2, order.Travellers.Count);
            Assert.True(service.RequiresReservation);
            Assert.Equal(ServiceDocumentKind.ElectronicMiscDocument, service.DocumentKind);
        }

        [Fact]
        public void New_air_transportation_cannot_be_added()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var travellerId = order.Travellers.First().Id;
            var segmentId = order.Segments.First().Id;

            var air = ProductAdditionFactory.Service(
                "AIR-NEW",
                OrderServiceType.AirTransportation,
                "AIR",
                new AcceptedAirTransportDetail($"{segmentId}"),
                [travellerId]);

            var addition = ProductAdditionFactory.Addition(
                ProductAdditionFactory.Product(ProductType.AirFare, [air]),
                [ProductAdditionFactory.Line(PricingComponentType.Fare, 1_000m, PricingBasisType.OrderService, "AIR-NEW")]);

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(addition), _ids, _clock));

            Assert.Equal(2852, exception.Code);
        }

        [Fact]
        public void The_addition_creates_no_traveller_segment_or_fare_construction()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var travellers = order.Travellers.Count;
            var segments = order.Segments.Count;
            var itineraries = order.Itineraries.Count;
            var constructions = order.FareConstructions.Count;

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);

            Assert.Equal(travellers, order.Travellers.Count);
            Assert.Equal(segments, order.Segments.Count);
            Assert.Equal(itineraries, order.Itineraries.Count);
            Assert.Equal(constructions, order.FareConstructions.Count);
        }

        [Fact]
        public void A_repeated_service_reference_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var bundle = ProductAdditionFactory.RoundTripBaggageBundle(order);

            var duplicated = bundle with
            {
                Product = bundle.Product with
                {
                    Services = [bundle.Product.Services[0], bundle.Product.Services[0]]
                }
            };

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(duplicated), _ids, _clock));

            Assert.Equal(2861, exception.Code);
        }

        private static AeroTech.Ordering.Domain.OrderAggregate.Arguments.AcceptedAddServiceChangeArgs WithService(
            Order order,
            Func<AcceptedAddedService, AcceptedAddedService> mutate,
            long operationId = ProductAdditionFactory.OperationId)
        {
            var accepted = ProductAdditionFactory.Seat(order);

            return ProductAdditionFactory.Args(
                accepted with
                {
                    Product = accepted.Product with { Services = [mutate(accepted.Product.Services[0])] }
                },
                operationId);
        }
    }
}
