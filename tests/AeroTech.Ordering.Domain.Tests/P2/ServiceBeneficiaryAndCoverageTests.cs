using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class ServiceBeneficiaryAndCoverageTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void An_air_service_has_exactly_one_beneficiary()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.AirTransportServices, service =>
            {
                Assert.Single(service.Beneficiaries);
                Assert.Contains(order.Travellers, traveller => traveller.Id == service.SoleBeneficiaryId);
            });
        }

        [Fact]
        public void A_seat_service_has_exactly_one_beneficiary()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat());

            var seat = Single(order, OrderServiceType.SeatAssignment);

            Assert.Single(seat.Beneficiaries);
            Assert.NotEqual(0L, seat.SoleBeneficiaryId);
        }

        [Fact]
        public void A_shared_service_carries_several_beneficiaries()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel());

            var hotel = Single(order, OrderServiceType.HotelStay);
            var travellerIds = order.Travellers.Select(traveller => traveller.Id).ToHashSet();

            Assert.Equal(2, hotel.Beneficiaries.Count);
            Assert.All(hotel.Beneficiaries, beneficiary => Assert.Contains(beneficiary.OrderTravellerId, travellerIds));
        }

        [Fact]
        public void A_service_with_no_beneficiary_is_rejected()
        {
            var orphan = AncillaryFactory.Meal("MEAL-ORPHAN");
            var noBeneficiary = orphan with { BeneficiaryTravellerRefs = [] };

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, noBeneficiary));

            Assert.Equal(2821, exception.Code);
        }

        [Fact]
        public void A_seat_service_with_two_beneficiaries_is_rejected()
        {
            var shared = AncillaryFactory.Seat() with { BeneficiaryTravellerRefs = ["T1", "T2"] };

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, shared));

            Assert.Equal(2822, exception.Code);
        }

        [Fact]
        public void Sole_beneficiary_access_on_a_shared_service_fails_closed()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel());

            var hotel = Single(order, OrderServiceType.HotelStay);

            var exception = Assert.Throws<BusinessException>(() => hotel.SoleBeneficiaryId);

            Assert.Equal(2822, exception.Code);
        }

        [Fact]
        public void An_unknown_beneficiary_reference_is_rejected()
        {
            var unknown = AncillaryFactory.Meal() with { BeneficiaryTravellerRefs = ["T9"] };

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, unknown));

            Assert.Equal(2784, exception.Code);
        }

        [Fact]
        public void A_repeated_beneficiary_reference_is_recorded_once()
        {
            var repeated = AncillaryFactory.Hotel() with { BeneficiaryTravellerRefs = ["T1", "T1", "T2"] };

            var order = AncillaryFactory.OrderWith(_ids, _clock, repeated);
            var hotel = Single(order, OrderServiceType.HotelStay);

            Assert.Equal(2, hotel.Beneficiaries.Count);
        }

        [Fact]
        public void A_service_knows_which_travellers_it_covers()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Hotel());

            var hotel = Single(order, OrderServiceType.HotelStay);
            var travellerIds = order.Travellers.Select(traveller => traveller.Id).ToList();

            Assert.All(travellerIds, id => Assert.True(hotel.CoversTraveller(id)));
            Assert.False(hotel.CoversTraveller(-1));
        }

        [Fact]
        public void Coverage_of_an_air_service_is_recorded_by_identity()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Baggage());

            var baggage = Single(order, OrderServiceType.BaggageAllowance);
            var airIds = order.AirTransportServices.Select(service => service.Id).ToHashSet();

            var covered = Assert.Single(baggage.CoveredServices);

            Assert.Contains(covered.CoveredOrderServiceId, airIds);
            Assert.Empty(baggage.CoveredSegments);
        }

        [Fact]
        public void Coverage_of_a_segment_is_recorded_by_identity()
        {
            var bySegment = AncillaryFactory.Baggage("BAG-SEG") with
            {
                CoveredAirServiceRefs = null,
                CoveredSegmentRefs = [AncillaryFactory.OutboundSegmentRef()]
            };

            var order = AncillaryFactory.OrderWith(_ids, _clock, bySegment);
            var baggage = Single(order, OrderServiceType.BaggageAllowance);
            var segmentIds = order.Segments.Select(segment => segment.Id).ToHashSet();

            var covered = Assert.Single(baggage.CoveredSegments);

            Assert.Contains(covered.OrderSegmentId, segmentIds);
            Assert.Empty(baggage.CoveredServices);
        }

        [Fact]
        public void Coverage_spanning_several_air_services_is_recorded_in_full()
        {
            var wide = AncillaryFactory.Baggage("BAG-WIDE") with
            {
                CoveredAirServiceRefs =
                [
                    AncillaryFactory.OutboundAirServiceRef(),
                    AncillaryFactory.InboundAirServiceRef()
                ]
            };

            var order = AncillaryFactory.OrderWith(_ids, _clock, wide);
            var baggage = Single(order, OrderServiceType.BaggageAllowance);

            Assert.Equal(2, baggage.CoveredServices.Count);
        }

        [Fact]
        public void An_unknown_covered_service_reference_is_rejected()
        {
            var broken = AncillaryFactory.Baggage() with { CoveredAirServiceRefs = ["NOT-A-SERVICE"] };

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, broken));

            Assert.Equal(2784, exception.Code);
        }

        [Fact]
        public void An_unknown_covered_segment_reference_is_rejected()
        {
            var broken = AncillaryFactory.Baggage() with
            {
                CoveredAirServiceRefs = null,
                CoveredSegmentRefs = ["NOT-A-SEGMENT"]
            };

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, broken));

            Assert.Equal(2784, exception.Code);
        }

        [Fact]
        public void A_non_air_service_cannot_be_declared_as_covered_air_service()
        {
            var seatRef = "SEAT-COVERED";

            var broken = AncillaryFactory.Baggage("BAG-BAD") with { CoveredAirServiceRefs = [seatRef] };

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Seat(seatRef), broken));

            Assert.Equal(2784, exception.Code);
        }

        [Fact]
        public void Coverage_is_never_inferred_when_the_source_states_none()
        {
            var noCoverage = AncillaryFactory.Baggage("BAG-NONE") with { CoveredAirServiceRefs = null };

            var order = AncillaryFactory.OrderWith(_ids, _clock, noCoverage);
            var baggage = Single(order, OrderServiceType.BaggageAllowance);

            Assert.Empty(baggage.CoveredServices);
            Assert.Empty(baggage.CoveredSegments);
        }

        [Fact]
        public void Coverage_and_beneficiaries_are_independent_facts()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Baggage());

            var baggage = Single(order, OrderServiceType.BaggageAllowance);

            var covered = Assert.Single(baggage.CoveredServices);

            Assert.Contains(order.Travellers, traveller => traveller.Id == baggage.SoleBeneficiaryId);
            Assert.Contains(order.AirTransportServices, air => air.Id == covered.CoveredOrderServiceId);
        }

        private static OrderService Single(Order order, OrderServiceType serviceType)
            => order.OrderServices.Single(service => service.ServiceType == serviceType);
    }
}
