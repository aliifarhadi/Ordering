using AeroTech.Messages.AirPrice.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Providers.Offer.Model;
using AeroTech.Ordering.Providers.Offer.Services;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    public sealed class ServicePriceTreatmentTests
    {
        private const decimal Fare = 1_000_000m;
        private const decimal Tax = 90_000m;

        private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        private readonly AirPriceOfferNormalizer _normalizer = new();
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_direct_service_level_fare_makes_the_service_separately_priced()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));

            Assert.All(AirServices(source), service =>
                Assert.Equal(ServicePriceTreatment.SeparatelyPriced, service.PriceTreatment));
        }

        [Fact]
        public void A_direct_service_level_product_charge_makes_the_service_separately_priced()
        {
            var lines = new[] { Line(PricingComponentType.ProductCharge, PricingBasisType.OrderService, serviceRef: "S1") };

            Assert.Equal(
                ServicePriceTreatment.SeparatelyPriced,
                ServicePriceTreatmentResolver.Resolve("S1", "P1", lines));
        }

        [Fact]
        public void An_item_level_fare_covering_two_air_services_leaves_both_included()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.TwoFlightOffer(
                Now,
                [
                    AirPriceOfferFixture.ItemFareLine(Fare),
                    AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)
                ]));

            var services = AirServices(source);

            Assert.Equal(2, services.Count);
            Assert.All(services, service => Assert.Equal(ServicePriceTreatment.Included, service.PriceTreatment));
        }

        [Fact]
        public void One_item_price_over_two_services_is_counted_once()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.TwoFlightOffer(
                Now,
                [
                    AirPriceOfferFixture.ItemFareLine(Fare),
                    AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)
                ]));

            var order = Create(source);

            Assert.Equal(Fare + Tax, order.CustomerTotal);
            Assert.Single(order.PricingLines, line => line.ComponentType == PricingComponentType.Fare);
            Assert.Equal(Fare, order.PricingLines.Single(line => line.ComponentType == PricingComponentType.Fare).SaleAmount);
        }

        [Fact]
        public void A_service_scoped_tax_alone_does_not_make_the_service_separately_priced()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.TwoFlightOffer(
                Now,
                [
                    AirPriceOfferFixture.ItemFareLine(Fare),
                    AirPriceOfferFixture.ChargeLine("TAX-1", "I6", 30_000m),
                    AirPriceOfferFixture.ChargeLineOn("TAX-1", "I6", 20_000m, AirPriceOfferFixture.SecondFlightId)
                ]));

            Assert.Contains(
                source.PricingLines,
                line => line.ComponentType == PricingComponentType.Tax && line.BasisType == PricingBasisType.OrderService);

            Assert.All(AirServices(source), service =>
                Assert.Equal(ServicePriceTreatment.Included, service.PriceTreatment));
        }

        [Fact]
        public void A_service_scoped_carrier_surcharge_alone_does_not_make_the_service_separately_priced()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines:
                [
                    AirPriceOfferFixture.ItemFareLine(Fare),
                    AirPriceOfferFixture.ChargeLine("YQ-1", "YQ", 40_000m)
                ],
                charges: [new OfferCharge("YQ-1", AirChargeKind.Surcharge, "YQ", "Carrier surcharge", false)]));

            Assert.Contains(
                source.PricingLines,
                line => line.ComponentType == PricingComponentType.CarrierSurcharge
                        && line.BasisType == PricingBasisType.OrderService);

            Assert.All(AirServices(source), service =>
                Assert.Equal(ServicePriceTreatment.Included, service.PriceTreatment));
        }

        [Fact]
        public void Missing_defensible_primary_pricing_evidence_becomes_supplier_opaque()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines: [AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)]));

            Assert.DoesNotContain(source.PricingLines, line => line.ComponentType == PricingComponentType.Fare);
            Assert.All(AirServices(source), service =>
                Assert.Equal(ServicePriceTreatment.SupplierOpaque, service.PriceTreatment));
        }

        [Fact]
        public void Complimentary_is_not_inferred_from_a_zero_amount()
        {
            var zeroPriced = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines:
                [
                    AirPriceOfferFixture.FareLine(0m),
                    AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)
                ]));

            Assert.All(AirServices(zeroPriced), service =>
                Assert.Equal(ServicePriceTreatment.SeparatelyPriced, service.PriceTreatment));

            Assert.Equal(
                ServicePriceTreatment.SupplierOpaque,
                ServicePriceTreatmentResolver.Resolve("S1", "P1", []));
        }

        [Fact]
        public void The_current_source_never_produces_complimentary()
        {
            var sources = new[]
            {
                _normalizer.Normalize(AirPriceOfferFixture.Offer(Now)),
                _normalizer.Normalize(AirPriceOfferFixture.Offer(
                    Now,
                    priceLines: [AirPriceOfferFixture.ItemFareLine(Fare), AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)])),
                _normalizer.Normalize(AirPriceOfferFixture.Offer(
                    Now,
                    priceLines: [AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)]))
            };

            foreach (var source in sources)
                Assert.DoesNotContain(AirServices(source), service => service.PriceTreatment == ServicePriceTreatment.Complimentary);
        }

        [Fact]
        public void Complimentary_survives_only_when_the_source_states_it_explicitly()
        {
            var order = AncillaryFactory.OrderWith(
                _ids,
                _clock,
                AncillaryFactory.Meal("MEAL-FREE", priceTreatment: ServicePriceTreatment.Complimentary));

            var meal = order.OrderServices.Single(service => service.ServiceType == OrderServiceType.Meal);

            Assert.Equal(ServicePriceTreatment.Complimentary, meal.PriceTreatment);
        }

        [Fact]
        public void Treatment_never_changes_the_customer_total()
        {
            var separatelyPriced = Create(_normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines: [AirPriceOfferFixture.FareLine(Fare), AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)])));

            var included = Create(_normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines: [AirPriceOfferFixture.ItemFareLine(Fare), AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)])));

            Assert.Equal(ServicePriceTreatment.SeparatelyPriced, Air(separatelyPriced).PriceTreatment);
            Assert.Equal(ServicePriceTreatment.Included, Air(included).PriceTreatment);
            Assert.Equal(separatelyPriced.CustomerTotal, included.CustomerTotal);
            Assert.Equal(Fare + Tax, included.CustomerTotal);
        }

        [Fact]
        public void Treatment_never_advances_the_financial_sequence()
        {
            var separatelyPriced = Create(_normalizer.Normalize(AirPriceOfferFixture.Offer(Now)));

            var included = Create(_normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines: [AirPriceOfferFixture.ItemFareLine(Fare), AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)])));

            Assert.Equal(separatelyPriced.FinancialSequence, included.FinancialSequence);
            Assert.Equal(1, included.FinancialSequence);
            Assert.Single(included.PriceChangeSets);
        }

        [Fact]
        public void Treatment_never_creates_or_removes_pricing_lines()
        {
            foreach (var offer in new[]
                     {
                         AirPriceOfferFixture.Offer(Now),
                         AirPriceOfferFixture.Offer(
                             Now,
                             priceLines: [AirPriceOfferFixture.ItemFareLine(Fare), AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)])
                     })
            {
                var source = _normalizer.Normalize(offer);
                var order = Create(source);

                Assert.Equal(source.PricingLines.Count, order.PricingLines.Count);
            }
        }

        private Order Create(AcceptedOrderSource source)
            => Order.Create(OrderFactory.Args(), source, OrderFactory.OwnerAirlineId, _ids, _clock);

        private static IReadOnlyList<AcceptedService> AirServices(AcceptedOrderSource source)
            => source.Products
                .SelectMany(product => product.Services)
                .Where(service => service.ServiceType == OrderServiceType.AirTransportation)
                .ToList();

        private static Domain.OrderAggregate.Entities.OrderService Air(Order order)
            => order.AirTransportServices.First();

        private static AcceptedSourcePricingLine Line(
            PricingComponentType componentType,
            PricingBasisType basisType,
            string? productRef = null,
            string? serviceRef = null,
            PricingEffect effect = PricingEffect.CustomerBalance,
            PricingLineRole lineRole = PricingLineRole.Original)
            => new(
                componentType,
                effect,
                OrderPricingLineDirection.Debit,
                lineRole,
                Fare,
                1,
                Fare,
                1,
                basisType,
                PricingApplicationLevel.PerSegment,
                RefundabilityRule.Refundable,
                "SRC-1",
                "1",
                productRef,
                serviceRef,
                "FARE",
                null,
                null);
    }
}
