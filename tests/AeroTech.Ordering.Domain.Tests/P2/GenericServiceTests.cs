using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class GenericServiceTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_registered_schema_produces_a_service_with_a_generic_detail()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.Priority());

            var priority = Of(order, OrderServiceType.Priority);

            Assert.NotNull(priority.GenericDetail);
            Assert.Equal("Priority", priority.GenericDetail!.SchemaName);
            Assert.Equal("1.0", priority.GenericDetail!.SchemaVersion);
            Assert.Equal(1, priority.AttachedDetailCount);
        }

        [Fact]
        public void Generic_attributes_are_stored_as_the_source_stated_them()
        {
            var order = AncillaryFactory.OrderWith(_ids, _clock, AncillaryFactory.WiFi());

            Assert.Equal("""{"accessKind":"FullFlight"}""", Of(order, OrderServiceType.WiFi).GenericDetail!.AttributesJson);
        }

        [Fact]
        public void An_unregistered_schema_is_rejected()
        {
            var unknown = AncillaryFactory.Generic("UNK-1", OrderServiceType.Other, "Telepathy", "1.0", "{}");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, unknown));

            Assert.Equal(20140, exception.Code);
        }

        [Fact]
        public void An_unsupported_schema_version_is_rejected()
        {
            var future = AncillaryFactory.Generic("PRI-9", OrderServiceType.Priority, "Priority", "9.9", """{"priorityKind":"Boarding"}""");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, future));

            Assert.Equal(20141, exception.Code);
        }

        [Fact]
        public void A_missing_required_attribute_is_rejected()
        {
            var incomplete = AncillaryFactory.Generic("PRI-2", OrderServiceType.Priority, "Priority", "1.0", "{}");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, incomplete));

            Assert.Equal(20142, exception.Code);
        }

        [Fact]
        public void A_null_required_attribute_is_rejected()
        {
            var nulled = AncillaryFactory.Generic("PRI-3", OrderServiceType.Priority, "Priority", "1.0", """{"priorityKind":null}""");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, nulled));

            Assert.Equal(20142, exception.Code);
        }

        [Fact]
        public void Attributes_that_are_not_valid_json_are_rejected()
        {
            var broken = AncillaryFactory.Generic("PRI-4", OrderServiceType.Priority, "Priority", "1.0", "not json");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, broken));

            Assert.Equal(20142, exception.Code);
        }

        [Fact]
        public void Attributes_that_are_not_a_json_object_are_rejected()
        {
            var broken = AncillaryFactory.Generic("PRI-5", OrderServiceType.Priority, "Priority", "1.0", "[1,2,3]");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, broken));

            Assert.Equal(20142, exception.Code);
        }

        [Fact]
        public void The_registered_schema_decides_the_fulfillment_profile_not_the_source()
        {
            var lying = AncillaryFactory.Priority() with
            {
                RequiresReservation = true,
                RequiresDocument = true,
                DocumentKind = ServiceDocumentKind.ElectronicTicket
            };

            var order = AncillaryFactory.OrderWith(_ids, _clock, lying);
            var priority = Of(order, OrderServiceType.Priority);

            Assert.False(priority.RequiresReservation);
            Assert.False(priority.RequiresDocument);
            Assert.Null(priority.DocumentKind);
        }

        [Fact]
        public void A_schema_bound_to_another_service_type_is_rejected()
        {
            var mismatched = AncillaryFactory.Generic("WIFI-BAD", OrderServiceType.Priority, "WiFi", "1.0", """{"accessKind":"FullFlight"}""");

            var exception = Assert.Throws<BusinessException>(
                () => AncillaryFactory.OrderWith(_ids, _clock, mismatched));

            Assert.Equal(20138, exception.Code);
        }

        [Fact]
        public void Every_registered_schema_declares_a_document_kind_only_when_it_requires_a_document()
        {
            foreach (var name in new[] { "Priority", "WiFi", "Cip", "SimCard", "ExtraSeat", "SpecialAssistance" })
            {
                var schema = GenericServiceSchemaRegistry.Resolve(name, "1.0");

                Assert.Equal(schema.RequiresDocument, schema.DocumentKind.HasValue);
            }
        }

        [Fact]
        public void A_generic_service_needs_no_product_catalogue()
        {
            var catalogueTypes = typeof(Order).Assembly.GetTypes()
                .Where(type => type.Name.Contains("Catalog", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.Empty(catalogueTypes);
        }

        [Fact]
        public void Several_generic_services_coexist_on_one_order()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Priority(),
                AncillaryFactory.WiFi(),
                AncillaryFactory.SimCard());

            Assert.Equal(3, order.OrderServices.Count(service => service.GenericDetail is not null));
        }

        private static OrderService Of(Order order, OrderServiceType serviceType)
            => order.OrderServices.Single(service => service.ServiceType == serviceType);
    }
}
