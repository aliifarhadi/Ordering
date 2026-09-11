using System.Reflection;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class AddProductPolicyTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_product_code_may_remain_null()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);
            var item = order.Items.Single(candidate => candidate.Id == added.OrderItemId);

            Assert.Null(item.ProductCode);
            Assert.Null(item.ProductSnapshot.ProductCode);
        }

        [Fact]
        public void A_product_name_may_remain_null()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);
            var item = order.Items.Single(candidate => candidate.Id == added.OrderItemId);

            Assert.Null(item.ProductName);
            Assert.Null(item.ProductSnapshot.ProductName);
        }

        [Fact]
        public void The_source_product_reference_stays_provenance()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);
            var snapshot = order.Items.Single(candidate => candidate.Id == added.OrderItemId).ProductSnapshot;

            Assert.Equal($"SRC-{ProductAdditionFactory.ProductRef}", snapshot.SourceProductReference);
            Assert.Equal(ProductAdditionFactory.SourceSystem, snapshot.SourceSystem);
            Assert.Equal(ProductAdditionFactory.SourceOfferId, snapshot.SourceOfferId);
            Assert.Null(snapshot.SourcePricingReference);
        }

        [Theory]
        [InlineData(ProductType.Penalty)]
        [InlineData(ProductType.ServiceFee)]
        [InlineData(ProductType.Credit)]
        [InlineData(ProductType.Voucher)]
        [InlineData(ProductType.TaxAdjustment)]
        [InlineData(ProductType.ManualAdjustment)]
        public void A_financial_pseudo_product_type_is_rejected(ProductType blocked)
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order);

            var pseudo = accepted with { Product = accepted.Product with { ProductType = blocked } };

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(pseudo), _ids, _clock));

            Assert.Equal(20154, exception.Code);
        }

        [Fact]
        public void An_ancillary_item_receives_no_air_transport_policy()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);
            var item = order.Items.Single(candidate => candidate.Id == added.OrderItemId);

            Assert.Null(item.PolicySnapshot);
        }

        [Fact]
        public void A_mixed_item_fabricates_no_per_passenger_segment_policy()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Hotel(order)), _ids, _clock);
            var item = order.Items.Single(candidate => candidate.Id == added.OrderItemId);
            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Null(item.PolicySnapshot);
            Assert.Equal(DeliveryModel.PerOrder, service.DeliveryModel);
            Assert.False(service.RequiresDocument);
            Assert.Null(service.SoldSegmentId);
        }

        [Fact]
        public void The_item_quantity_comes_from_the_source_and_not_from_the_service_count()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order)),
                _ids,
                _clock);

            var item = order.Items.Single(candidate => candidate.Id == added.OrderItemId);

            Assert.Equal(1m, item.Quantity);
            Assert.Equal(OrderItemUnitOfMeasure.Each, item.UnitOfMeasure);
            Assert.Equal(2, added.OrderServiceIds.Count);
        }

        [Fact]
        public void A_zero_item_quantity_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order);

            var exception = Assert.Throws<BusinessException>(() => order.AddProduct(
                ProductAdditionFactory.Args(accepted with { Product = accepted.Product with { Quantity = 0m } }),
                _ids,
                _clock));

            Assert.Equal(20163, exception.Code);
        }

        [Fact]
        public void A_product_without_a_service_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order);

            var exception = Assert.Throws<BusinessException>(() => order.AddProduct(
                ProductAdditionFactory.Args(accepted with { Product = accepted.Product with { Services = [] } }),
                _ids,
                _clock));

            Assert.Equal(20158, exception.Code);
        }

        [Fact]
        public void The_product_snapshot_is_immutable()
        {
            var writable = typeof(AeroTech.Ordering.Domain.OrderAggregate.Entities.OrderItemProductSnapshot)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .ToList();

            Assert.Empty(writable);
        }

        [Fact]
        public void The_commercial_terms_snapshot_is_immutable_and_semantic()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);
            var terms = order.Items.Single(candidate => candidate.Id == added.OrderItemId).CommercialTermsSnapshot;

            Assert.Equal(CommercialTermState.Conditional, terms.RefundabilitySummary);
            Assert.Equal(CommercialTermState.Prohibited, terms.ChangeabilitySummary);
            Assert.Equal(CommercialTermState.Unknown, terms.UpgradeEligibilitySummary);

            var writable = typeof(AeroTech.Ordering.Domain.OrderAggregate.Entities.OrderItemCommercialTermsSnapshot)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.SetMethod is { IsPublic: true })
                .ToList();

            Assert.Empty(writable);
        }

        [Fact]
        public void A_blocked_service_type_cannot_be_added()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var travellerId = order.Travellers.First().Id;

            var pseudo = ProductAdditionFactory.Service(
                "FEE-1",
                OrderServiceType.ServiceFee,
                "FEE",
                new AcceptedMealDetail("VGML", 1),
                [travellerId],
                ServicePriceTreatment.SupplierOpaque);

            var addition = ProductAdditionFactory.Addition(
                ProductAdditionFactory.Product(ProductType.Ancillary, [pseudo]),
                [ProductAdditionFactory.Line(PricingComponentType.Fee, 1_000m, PricingBasisType.OrderItem)]);

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(addition), _ids, _clock));

            Assert.Equal(20134, exception.Code);
        }

        [Fact]
        public void A_generic_service_must_satisfy_the_registered_schema()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.WiFi(order);

            var broken = accepted with
            {
                Product = accepted.Product with
                {
                    Services =
                    [
                        accepted.Product.Services[0] with
                        {
                            Detail = new AcceptedGenericServiceDetail("WiFi", "1.0", "{}")
                        }
                    ]
                }
            };

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(broken), _ids, _clock));

            Assert.Equal(20142, exception.Code);
        }

        [Fact]
        public void A_registered_generic_schema_supplies_the_fulfillment_profile()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.WiFi(order)), _ids, _clock);
            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Equal(OrderServiceType.WiFi, service.ServiceType);
            Assert.False(service.RequiresReservation);
            Assert.False(service.RequiresDocument);
            Assert.Null(service.DocumentKind);
        }
    }
}
