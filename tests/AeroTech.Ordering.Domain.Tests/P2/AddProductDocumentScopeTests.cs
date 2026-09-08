using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class AddProductDocumentScopeTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void An_emd_required_ancillary_can_be_added_to_a_ticketed_air_order()
        {
            var order = TicketedOrder();

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order)), _ids, _clock);

            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.True(service.RequiresDocument);
            Assert.Equal(ServiceDocumentKind.ElectronicMiscDocument, service.DocumentKind);
            Assert.Equal(OrderServiceStatus.Active, service.Status);
        }

        [Fact]
        public void The_existing_electronic_ticket_evidence_is_unchanged()
        {
            var order = TicketedOrder();

            var before = order.AirTransportServices
                .Select(service => (service.Id, service.ElectronicTicketId, service.TicketCouponId, service.DocumentStatus))
                .OrderBy(entry => entry.Id)
                .ToList();

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order)), _ids, _clock);

            var after = order.AirTransportServices
                .Select(service => (service.Id, service.ElectronicTicketId, service.TicketCouponId, service.DocumentStatus))
                .OrderBy(entry => entry.Id)
                .ToList();

            Assert.Equal(before, after);
        }

        [Fact]
        public void Electronic_ticketing_stays_complete_after_adding_a_pending_emd_ancillary()
        {
            var order = TicketedOrder();

            Assert.True(order.IsElectronicTicketingComplete());

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order)), _ids, _clock);

            Assert.True(order.IsElectronicTicketingComplete());
        }

        [Fact]
        public void The_legacy_ticketed_status_does_not_downgrade()
        {
            var order = TicketedOrder();

            Assert.Equal(OrderStatus.Ticketed, order.Status);

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order)), _ids, _clock);

            Assert.Equal(OrderStatus.Ticketed, order.Status);
        }

        [Fact]
        public void The_added_emd_service_stays_pending()
        {
            var order = TicketedOrder();

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order)), _ids, _clock);

            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Equal(OrderServiceDocumentStatus.Pending, service.DocumentStatus);
            Assert.Null(service.TrafficDocumentId);
            Assert.Null(service.DocumentCouponId);
            Assert.Null(service.ElectronicTicketId);
            Assert.Null(service.TicketCouponId);
        }

        [Fact]
        public void An_emd_service_is_outside_the_electronic_ticket_scope()
        {
            var order = TicketedOrder();

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order)), _ids, _clock);
            var serviceId = added.OrderServiceIds.Single();

            Assert.DoesNotContain(serviceId, order.RequiredElectronicTicketServiceIds());
            Assert.Contains(serviceId, order.RequiredDocumentServiceIds());
            Assert.DoesNotContain(serviceId, order.DocumentedElectronicTicketServiceIds());
        }

        [Fact]
        public void An_added_service_is_never_auto_reserved_or_auto_documented()
        {
            var order = TicketedOrder();

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.ExtraSeat(order)), _ids, _clock);
            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.True(service.RequiresReservation);
            Assert.Equal(OrderFulfillmentStatus.Pending, service.FulfillmentStatus);
            Assert.Equal(OrderServiceDocumentStatus.Pending, service.DocumentStatus);
            Assert.Null(service.HoldBatchId);
            Assert.Null(service.SeatHoldReference);
        }

        [Fact]
        public void Adding_a_product_does_not_touch_unrelated_historical_services()
        {
            var order = TicketedOrder();

            var before = order.AirTransportServices
                .Select(service => (service.Id, service.Status, service.FulfillmentStatus, service.CommercialStatus))
                .OrderBy(entry => entry.Id)
                .ToList();

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);

            var after = order.AirTransportServices
                .Select(service => (service.Id, service.Status, service.FulfillmentStatus, service.CommercialStatus))
                .OrderBy(entry => entry.Id)
                .ToList();

            Assert.Equal(before, after);
        }

        private Order TicketedOrder()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var serviceIds = order.OrderServices.Select(service => service.Id).ToList();

            order.ApplyReservationOutcome(serviceIds, "PNR-1", null, _ids, _clock);
            order.RecordIssuedDocuments(
                serviceIds.Select(id => new IssuedServiceDocument(id, 900_000 + id, 800_000 + id)).ToList());
            order.CompleteTicketing(_clock);

            return order;
        }
    }
}
