using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class FareConstructionScopeTests
    {
        private const string OutboundJourney = "B1";
        private const string InboundJourney = "B2";

        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void Two_independent_constructions_are_both_current()
        {
            var order = WithConstructions(OutboundOnly("A", "YOUT"), InboundOnly("B", "YIN"));

            Assert.Equal(2, order.FareConstructions.Count);
            Assert.Equal(2, order.CurrentFareConstructions().Count);
        }

        [Fact]
        public void Each_service_resolves_its_fare_component_from_its_own_construction()
        {
            var order = WithConstructions(OutboundOnly("A", "YOUT"), InboundOnly("B", "YIN"));

            Assert.Equal("YOUT", order.ResolveIssueFareBasis(ServiceId(order, OutboundJourney)));
            Assert.Equal("YIN", order.ResolveIssueFareBasis(ServiceId(order, InboundJourney)));
        }

        [Fact]
        public void Creation_order_and_created_at_do_not_decide_service_resolution()
        {
            var earlyFirst = WithConstructions(OutboundOnly("A", "YOUT"), InboundOnly("B", "YIN"));

            _clock.Advance(TimeSpan.FromHours(1));

            var lateFirst = WithConstructions(InboundOnly("B", "YIN"), OutboundOnly("A", "YOUT"));

            foreach (var order in new[] { earlyFirst, lateFirst })
            {
                Assert.Equal("YOUT", order.ResolveIssueFareBasis(ServiceId(order, OutboundJourney)));
                Assert.Equal("YIN", order.ResolveIssueFareBasis(ServiceId(order, InboundJourney)));
            }
        }

        [Fact]
        public void Superseding_one_branch_leaves_the_unrelated_branch_current()
        {
            var order = WithConstructions(
                OutboundOnly("A1", "YOUT-1"),
                InboundOnly("B1", "YIN"),
                OutboundOnly("A2", "YOUT-2", supersedes: "A1"));

            var current = order.CurrentFareConstructions();

            Assert.Equal(2, current.Count);
            Assert.Equal(3, order.FareConstructions.Count);

            var supersededId = order.FareConstructions
                .Single(construction => construction.SupersedesConstructionId.HasValue)
                .SupersedesConstructionId;

            Assert.DoesNotContain(current, construction => construction.Id == supersededId);
            Assert.Contains(current, construction => construction.FareComponents.Any(component => component.FareBasis == "YIN"));
        }

        [Fact]
        public void A_superseded_construction_is_ignored_when_resolving_fare_context()
        {
            var order = WithConstructions(
                OutboundOnly("A1", "YOUT-1"),
                OutboundOnly("A2", "YOUT-2", supersedes: "A1"));

            Assert.Equal("YOUT-2", order.ResolveIssueFareBasis(ServiceId(order, OutboundJourney)));
        }

        [Fact]
        public void A_service_with_no_active_component_uses_the_transitional_fallback()
        {
            var order = WithConstructions(OutboundOnly("A", "YOUT"));

            var inbound = ServiceId(order, InboundJourney);
            var service = order.OrderServices.Where(service => service.IsAirTransport).Single(candidate => candidate.Id == inbound);

            Assert.Null(order.ActiveFareComponentFor(inbound));
            Assert.Equal(service.AirTransportDetail?.TransitionalFareBasis, order.ResolveIssueFareBasis(inbound));
        }

        [Fact]
        public void Exactly_one_active_component_wins()
        {
            var order = WithConstructions(OutboundOnly("A", "YOUT"), InboundOnly("B", "YIN"));
            var outbound = ServiceId(order, OutboundJourney);

            var component = order.ActiveFareComponentFor(outbound);

            Assert.NotNull(component);
            Assert.Equal("YOUT", component!.FareBasis);
        }

        [Fact]
        public void Two_active_components_covering_one_service_fail_closed()
        {
            var order = WithConstructions(OutboundOnly("A", "YOUT-A"), OutboundOnly("B", "YOUT-B"));
            var outbound = ServiceId(order, OutboundJourney);

            var exception = Assert.Throws<BusinessException>(() => order.ActiveFareComponentFor(outbound));

            Assert.Equal(20133, exception.Code);
        }

        [Fact]
        public void Ambiguity_never_silently_falls_back_to_the_legacy_fare_basis()
        {
            var order = WithConstructions(OutboundOnly("A", "YOUT-A"), OutboundOnly("B", "YOUT-B"));
            var outbound = ServiceId(order, OutboundJourney);
            var service = order.OrderServices.Where(service => service.IsAirTransport).Single(candidate => candidate.Id == outbound);

            var exception = Assert.Throws<BusinessException>(() => order.ResolveIssueFareBasis(outbound));

            Assert.Equal(20133, exception.Code);
            Assert.NotNull(service.AirTransportDetail?.TransitionalFareBasis);
        }

        [Fact]
        public void A_through_fare_component_still_covers_several_services_unambiguously()
        {
            var order = WithConstructions(FareConstructionFactory.ThroughFare());

            var outbound = ServiceId(order, OutboundJourney);
            var inbound = ServiceId(order, InboundJourney);

            Assert.Equal("YTHRU", order.ResolveIssueFareBasis(outbound));
            Assert.Equal("YTHRU", order.ResolveIssueFareBasis(inbound));
            Assert.Equal(order.ActiveFareComponentFor(outbound)!.Id, order.ActiveFareComponentFor(inbound)!.Id);
        }

        private static AcceptedFareConstruction OutboundOnly(string reference, string fareBasis, string? supersedes = null)
            => FareConstructionFactory.SingleService(
                reference,
                "T1",
                OutboundJourney,
                MultiPassengerOrderFactory.OutboundFlightId,
                fareBasis,
                supersedes);

        private static AcceptedFareConstruction InboundOnly(string reference, string fareBasis, string? supersedes = null)
            => FareConstructionFactory.SingleService(
                reference,
                "T1",
                InboundJourney,
                MultiPassengerOrderFactory.InboundFlightId,
                fareBasis,
                supersedes);

        private static long ServiceId(Order order, string journeyRef)
        {
            var flightId = journeyRef == OutboundJourney
                ? MultiPassengerOrderFactory.OutboundFlightId
                : MultiPassengerOrderFactory.InboundFlightId;

            var traveller = order.Travellers.OrderBy(candidate => candidate.Index).First();
            var segmentIds = order.Segments.Where(segment => segment.FlightId == flightId).Select(segment => segment.Id).ToHashSet();

            return order.OrderServices
                .Where(service => service.IsAirTransport)
                .Single(service => service.SoleBeneficiaryId == traveller.Id && segmentIds.Contains(service.SoldSegmentId!.Value))
                .Id;
        }

        private Order WithConstructions(params AcceptedFareConstruction[] constructions)
        {
            var source = MultiPassengerOrderFactory.AcceptedSource(_clock) with
            {
                FareConstructions = constructions
            };

            return Order.Create(
                MultiPassengerOrderFactory.Args(),
                source,
                MultiPassengerOrderFactory.OwnerAirlineId,
                _ids,
                _clock);
        }
    }
}
