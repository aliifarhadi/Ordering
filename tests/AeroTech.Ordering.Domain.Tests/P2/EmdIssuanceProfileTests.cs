using System.Reflection;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class EmdIssuanceProfileTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void An_accepted_profile_is_snapshotted_on_the_added_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var air = ProductAdditionFactory.OutboundAirService(order);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order)), _ids, _clock);
            var snapshot = Service(order, added.OrderServiceIds.Single()).EmdIssuanceSnapshot;

            Assert.NotNull(snapshot);
            Assert.Equal(ElectronicMiscDocumentType.Associated, snapshot!.EmdType);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceCode, snapshot.ReasonForIssuanceCode);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceSubCode, snapshot.ReasonForIssuanceSubCode);
            Assert.Equal(air.Id, snapshot.AssociatedAirOrderServiceId);
            Assert.Equal(ProductAdditionFactory.SourceSystem, snapshot.SourceSystem);
            Assert.Equal(ProductAdditionFactory.QuotedOfferId, snapshot.SourceReference);
        }

        [Fact]
        public void A_standalone_profile_carries_no_air_association()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.EmdLounge(order)), _ids, _clock);
            var snapshot = Service(order, added.OrderServiceIds.Single()).EmdIssuanceSnapshot!;

            Assert.Equal(ElectronicMiscDocumentType.Standalone, snapshot.EmdType);
            Assert.Null(snapshot.AssociatedAirOrderServiceId);
        }

        [Fact]
        public void An_associated_profile_without_an_air_service_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.EmdBaggage(order);

            var broken = WithProfile(accepted, ProductAdditionFactory.IssuanceProfile(
                ElectronicMiscDocumentType.Associated,
                "C",
                "0DF",
                associatedAirOrderServiceId: null));

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(broken), _ids, _clock));

            Assert.Equal(20180, exception.Code);
        }

        [Fact]
        public void A_blank_reason_for_issuance_code_is_rejected_at_sale()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var air = ProductAdditionFactory.OutboundAirService(order);
            var accepted = ProductAdditionFactory.EmdBaggage(order);

            var broken = WithProfile(accepted, ProductAdditionFactory.IssuanceProfile(
                ElectronicMiscDocumentType.Associated, "  ", "0DF", air.Id));

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(broken), _ids, _clock));

            Assert.Equal(20169, exception.Code);
        }

        [Fact]
        public void A_blank_reason_for_issuance_sub_code_is_rejected_at_sale()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var air = ProductAdditionFactory.OutboundAirService(order);
            var accepted = ProductAdditionFactory.EmdBaggage(order);

            var broken = WithProfile(accepted, ProductAdditionFactory.IssuanceProfile(
                ElectronicMiscDocumentType.Associated, "C", " ", air.Id));

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(broken), _ids, _clock));

            Assert.Equal(20170, exception.Code);
        }

        [Fact]
        public void A_profile_cannot_be_attached_to_a_service_that_needs_no_miscellaneous_document()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var air = ProductAdditionFactory.OutboundAirService(order);
            var accepted = ProductAdditionFactory.Seat(order);

            var broken = WithProfile(accepted, ProductAdditionFactory.IssuanceProfile(
                ElectronicMiscDocumentType.Associated, "C", "0DF", air.Id));

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(broken), _ids, _clock));

            Assert.Equal(20177, exception.Code);
        }

        [Fact]
        public void The_reason_for_issuance_code_is_not_inferred_from_the_product_type()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order, reasonForIssuanceCode: "I")),
                _ids,
                _clock);

            var item = order.Items.Single(candidate => candidate.Id == added.OrderItemId);
            var snapshot = Service(order, added.OrderServiceIds.Single()).EmdIssuanceSnapshot!;

            Assert.Equal(ProductType.Baggage, item.ProductType);
            Assert.Equal("I", snapshot.ReasonForIssuanceCode);
        }

        [Fact]
        public void The_reason_for_issuance_sub_code_is_not_derived_from_the_service_code()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order, reasonForIssuanceSubCode: "0XX")),
                _ids,
                _clock);

            var service = Service(order, added.OrderServiceIds.Single());

            Assert.Equal("BAG", service.ServiceCode);
            Assert.Equal("0XX", service.EmdIssuanceSnapshot!.ReasonForIssuanceSubCode);
        }

        [Fact]
        public void The_emd_type_is_not_inferred_from_the_service_type()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var standaloneBaggage = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(
                    order,
                    emdType: ElectronicMiscDocumentType.Standalone)),
                _ids,
                _clock);

            Assert.Equal(
                ElectronicMiscDocumentType.Standalone,
                Service(order, standaloneBaggage.OrderServiceIds.Single()).EmdIssuanceSnapshot!.EmdType);
        }

        [Fact]
        public void A_service_may_be_sold_without_a_profile_and_is_then_not_issuable()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.EmdBaggage(order, withIssuanceProfile: false)),
                _ids,
                _clock);

            var service = Service(order, added.OrderServiceIds.Single());

            Assert.Null(service.EmdIssuanceSnapshot);
            Assert.True(service.RequiresDocument);
            Assert.Equal(ServiceDocumentKind.ElectronicMiscDocument, service.DocumentKind);
        }

        [Fact]
        public void The_snapshot_is_immutable()
        {
            var writable = typeof(OrderServiceEmdIssuanceSnapshot)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .ToList();

            Assert.Empty(writable);
        }

        [Fact]
        public void A_generic_service_schema_is_not_declared_as_the_special_service_request_model()
        {
            var schema = GenericServiceSchemaRegistry.Resolve("SpecialAssistance", "1.0");

            Assert.DoesNotContain("ssr", schema.SchemaName, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(schema.RequiredAttributes, attribute => attribute.Contains("ssr", StringComparison.OrdinalIgnoreCase));
        }

        private static OrderService Service(Order order, long serviceId)
            => order.OrderServices.Single(service => service.Id == serviceId);

        private static Domain.OrderAggregate.AcceptedSource.ProductAddition.AcceptedAddServiceChange WithProfile(
            Domain.OrderAggregate.AcceptedSource.ProductAddition.AcceptedAddServiceChange accepted,
            Domain.OrderAggregate.AcceptedSource.ProductAddition.AcceptedEmdIssuanceProfile profile)
            => accepted with
            {
                Product = accepted.Product with
                {
                    Services = [accepted.Product.Services[0] with { EmdIssuance = profile }]
                }
            };
    }
}
