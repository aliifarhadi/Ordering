using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P1
{
    public sealed class OrderStructureTests
    {
        private readonly SequentialIdGenerator _ids = new();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_two_traveler_round_trip_creates_a_service_per_traveler_and_sold_segment()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.Equal(2, order.Travellers.Count);
            Assert.Equal(2, order.Itineraries.Count);
            Assert.Equal(2, order.Segments.Count);
            Assert.Equal(4, order.OrderServices.OfType<OrderAirTransportService>().Count());
        }

        [Fact]
        public void Commercial_grouping_follows_the_priced_boundary_not_the_passenger_or_segment_count()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.NotEqual(order.Travellers.Count, order.Items.Count);
            Assert.NotEqual(order.Segments.Count, order.Items.Count);
            Assert.All(order.Items, item => Assert.NotEmpty(order.OrderServices.Where(service => service.OrderItemId == item.Id)));
            Assert.All(order.OrderServices, service => Assert.Single(order.Items.Where(item => item.Id == service.OrderItemId)));
        }

        [Fact]
        public void One_order_item_carries_several_services_when_the_source_priced_them_together()
        {
            var order = MultiPassengerOrderFactory.CreateWithThroughFare(_ids, _clock);

            Assert.Equal(2, order.Items.Count);
            Assert.Equal(4, order.OrderServices.Count);
            Assert.All(order.Items, item =>
                Assert.Equal(2, order.OrderServices.Count(service => service.OrderItemId == item.Id)));
        }

        [Fact]
        public void Every_air_service_binds_exactly_one_traveler_and_one_sold_segment()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            foreach (var service in order.OrderServices.OfType<OrderAirTransportService>())
            {
                Assert.Single(order.Travellers.Where(traveller => traveller.Id == service.TravellerId));
                Assert.Single(order.Segments.Where(segment => segment.Id == service.OrderSegmentId));
            }
        }

        [Fact]
        public void Service_identity_is_stable_and_unique_across_the_order()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var ids = order.OrderServices.Select(service => service.Id).ToList();

            Assert.Equal(ids.Count, ids.Distinct().Count());
            Assert.All(ids, id => Assert.True(id > 0));
        }

        [Fact]
        public void Creation_establishes_commercial_version_one_and_an_active_commercial_summary()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.Equal(1, order.CommercialVersion);
            Assert.Equal(CommercialSummary.Active, order.CommercialSummary);
        }

        [Fact]
        public void Creation_records_lineage_rooted_at_the_order_itself()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.Equal(order.Id, order.Lineage.RootOrderId);
            Assert.Null(order.Lineage.ParentOrderId);
        }

        [Fact]
        public void The_sold_schedule_snapshot_is_retained_on_each_segment()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.Segments, segment =>
            {
                Assert.False(string.IsNullOrWhiteSpace(segment.Number));
                Assert.True(segment.DepartureDateTime < segment.ArrivalDateTime);
                Assert.True(segment.MarketingAirlineId > 0);
            });
        }

        [Fact]
        public void No_financial_pseudo_service_is_created_for_taxes_or_fees()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.OrderServices, service =>
                Assert.Equal(OrderServiceType.AirTransportation, service.ServiceType));

            Assert.Contains(order.PricingLines, line => line.LineCategory == OrderPricingLineCategory.Tax);
        }

        [Fact]
        public void A_reservation_outcome_does_not_advance_the_commercial_version()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var services = order.OrderServices.Select(service => service.Id).ToList();

            order.ApplyReservationOutcome(services, "PNR-1", _clock.GetDateTime().AddHours(6), _ids, _clock);

            Assert.Equal(1, order.CommercialVersion);
            Assert.Equal(CommercialSummary.Active, order.CommercialSummary);
            Assert.NotNull(order.ActiveTimeLimit(TimeLimitType.Ticketing));
        }

        [Fact]
        public void Issuing_documents_does_not_advance_the_commercial_version()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var services = order.OrderServices.Select(service => service.Id).ToList();

            order.ApplyReservationOutcome(services, "PNR-1", null, _ids, _clock);
            order.ApplyIssuedDocuments(
                services.Select(id => new Domain.OrderAggregate.IssuedServiceDocument(id, 900 + id, 800 + id)).ToList(),
                _clock);

            Assert.Equal(1, order.CommercialVersion);
            Assert.All(order.OrderServices, service => Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus));
        }

        [Fact]
        public void Pre_ticket_withdrawal_is_one_commercial_mutation()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var services = order.OrderServices.Select(service => service.Id).ToList();

            order.WithdrawBeforeTicketing(services, VoidReason.CustomerRequest, 7, _ids, _clock);

            Assert.Equal(2, order.CommercialVersion);
            Assert.Equal(CommercialSummary.Cancelled, order.CommercialSummary);
            Assert.Single(order.GetEvents().OfType<Domain.OrderAggregate.DomainEvents.OrderWithdrawn>());
        }

        [Fact]
        public void An_external_reservation_reference_is_recorded_once()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var services = order.OrderServices.Select(service => service.Id).ToList();

            order.ApplyReservationOutcome(services, "PNR-1", null, _ids, _clock);
            order.ApplyReservationOutcome(services, "PNR-1", null, _ids, _clock);

            Assert.Single(order.ExternalReferences.Where(reference =>
                reference.Type == ExternalReferenceType.ProviderReservation));
        }
    }
}
