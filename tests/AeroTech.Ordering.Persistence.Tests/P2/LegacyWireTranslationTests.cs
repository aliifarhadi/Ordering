using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.EventHandlers;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    public sealed class LegacyWireTranslationTests
    {
        [Fact]
        public void An_active_v1_sale_debit_is_published_as_the_legacy_credit_direction()
        {
            Assert.Equal(
                OrderPricingLineDirection.Credit,
                LegacyPricingLineTranslation.Direction(OrderPricingLineDirection.Debit));
        }

        [Fact]
        public void An_active_v1_credit_is_published_as_the_legacy_debit_direction()
        {
            Assert.Equal(
                OrderPricingLineDirection.Debit,
                LegacyPricingLineTranslation.Direction(OrderPricingLineDirection.Credit));
        }

        [Theory]
        [InlineData(PricingComponentType.Fare, OrderPricingLineCategory.Fare)]
        [InlineData(PricingComponentType.Tax, OrderPricingLineCategory.Tax)]
        [InlineData(PricingComponentType.CarrierSurcharge, OrderPricingLineCategory.CarrierImposedSurcharge)]
        [InlineData(PricingComponentType.Fee, OrderPricingLineCategory.Fee)]
        [InlineData(PricingComponentType.Discount, OrderPricingLineCategory.Discount)]
        [InlineData(PricingComponentType.Commission, OrderPricingLineCategory.Commission)]
        [InlineData(PricingComponentType.ProductCharge, OrderPricingLineCategory.Ancillary)]
        public void The_component_type_maps_onto_the_legacy_wire_category(
            PricingComponentType componentType,
            OrderPricingLineCategory expected)
        {
            Assert.Equal(expected, LegacyPricingLineTranslation.Category(componentType));
        }
    }
}
