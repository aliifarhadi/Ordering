using System.Reflection;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class AirServiceDetailTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void The_air_detail_holds_the_sold_segment_identity()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var segmentIds = order.Segments.Select(segment => segment.Id).ToHashSet();

            Assert.All(order.AirTransportServices, service =>
                Assert.Contains(service.AirTransportDetail!.OrderSegmentId, segmentIds));
        }

        [Fact]
        public void One_air_service_exists_per_traveller_and_segment()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var pairs = order.AirTransportServices
                .Select(service => (service.SoleBeneficiaryId, service.SoldSegmentId))
                .ToList();

            Assert.Equal(4, pairs.Count);
            Assert.Equal(pairs.Count, pairs.Distinct().Count());
        }

        [Fact]
        public void The_air_detail_keeps_the_transitional_fare_basis()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.AirTransportServices, service =>
                Assert.Equal(MultiPassengerOrderFactory.FareBasis, service.AirTransportDetail!.TransitionalFareBasis));
        }

        [Fact]
        public void The_air_detail_records_a_requested_seat_only_when_the_source_states_one()
        {
            var withoutSeat = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(withoutSeat.AirTransportServices, service =>
                Assert.Null(service.AirTransportDetail!.RequestedSeat));

            var order = CreateFrom(AncillaryFactory.MapAirDetails(
                MultiPassengerOrderFactory.AcceptedSource(_clock),
                air => air with { RequestedSeat = "14C" }));

            Assert.All(order.AirTransportServices, service =>
                Assert.Equal("14C", service.AirTransportDetail!.RequestedSeat));
        }

        [Fact]
        public void The_air_detail_preserves_transitional_baggage_evidence_from_the_source()
        {
            var order = CreateFrom(AncillaryFactory.MapAirDetails(
                MultiPassengerOrderFactory.AcceptedSource(_clock),
                air => air with
                {
                    TransitionalCheckedBaggage = new AcceptedBaggageAllowance(2, 23m, BaggageWeightUnit.Kg),
                    TransitionalCabinBaggage = new AcceptedBaggageAllowance(1, 7m, BaggageWeightUnit.Kg)
                }));

            var detail = order.AirTransportServices.First().AirTransportDetail!;

            Assert.Equal(2, detail.TransitionalCheckedBaggage!.Pieces);
            Assert.Equal(23m, detail.TransitionalCheckedBaggage!.Weight);
            Assert.Equal(BaggageWeightUnit.Kg, detail.TransitionalCheckedBaggage!.Unit);
            Assert.Equal(1, detail.TransitionalCabinBaggage!.Pieces);
            Assert.Equal(7m, detail.TransitionalCabinBaggage!.Weight);
        }

        [Fact]
        public void The_air_detail_invents_no_baggage_when_the_source_states_none()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.AirTransportServices, service =>
            {
                Assert.Null(service.AirTransportDetail!.TransitionalCheckedBaggage);
                Assert.Null(service.AirTransportDetail!.TransitionalCabinBaggage);
            });
        }

        [Fact]
        public void Transitional_baggage_evidence_does_not_create_a_baggage_service()
        {
            var order = CreateFrom(AncillaryFactory.MapAirDetails(
                MultiPassengerOrderFactory.AcceptedSource(_clock),
                air => air with
                {
                    TransitionalCheckedBaggage = new AcceptedBaggageAllowance(2, 23m, BaggageWeightUnit.Kg)
                }));

            Assert.DoesNotContain(order.OrderServices, service => service.ServiceType == OrderServiceType.BaggageAllowance);
        }

        [Fact]
        public void The_air_service_requires_a_reservation_and_an_electronic_ticket()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.AirTransportServices, service =>
            {
                Assert.True(service.RequiresReservation);
                Assert.True(service.RequiresDocument);
                Assert.Equal(ServiceDocumentKind.ElectronicTicket, service.DocumentKind);
            });
        }

        [Fact]
        public void The_air_detail_owns_no_fare_family_and_no_commercial_policy()
        {
            var names = typeof(OrderAirTransportServiceDetail)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToList();

            Assert.DoesNotContain("FareFamily", names);
            Assert.DoesNotContain("FareFamilyCode", names);
            Assert.DoesNotContain("IsRefundable", names);
            Assert.DoesNotContain("IsChangeable", names);
            Assert.DoesNotContain("IsUpgradable", names);
        }

        [Fact]
        public void An_air_service_without_a_resolvable_segment_is_rejected()
        {
            var source = AncillaryFactory.MapAirDetails(
                MultiPassengerOrderFactory.AcceptedSource(_clock),
                air => air with { SegmentRef = "NOT-A-SEGMENT" });

            var exception = Assert.Throws<Framework.Core.Domain.Exceptions.BusinessException>(() => CreateFrom(source));

            Assert.Equal(2784, exception.Code);
        }

        private Order CreateFrom(AcceptedOrderSource source)
            => Order.Create(
                MultiPassengerOrderFactory.Args(),
                source,
                MultiPassengerOrderFactory.OwnerAirlineId,
                _ids,
                _clock);
    }
}
