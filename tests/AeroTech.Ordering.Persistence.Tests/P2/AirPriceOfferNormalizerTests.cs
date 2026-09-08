using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.AirPrice.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Providers.Offer.Model;
using AeroTech.Ordering.Providers.Offer.Services;
using Xunit;
using BoundDirection = AeroTech.Messages.Ordering.Enums.BoundDirection;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    public sealed class AirPriceOfferNormalizerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        private readonly AirPriceOfferNormalizer _normalizer = new();

        [Theory]
        [InlineData(AirChargeKind.Tax, PricingComponentType.Tax)]
        [InlineData(AirChargeKind.Surcharge, PricingComponentType.CarrierSurcharge)]
        [InlineData(AirChargeKind.Fee, PricingComponentType.Fee)]
        public void Each_supported_source_charge_kind_maps_to_its_accepted_component(
            AirChargeKind kind,
            PricingComponentType expected)
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                charges: [new OfferCharge("TAX-1", kind, "I6", "Charge", true)]));

            Assert.Contains(source.PricingLines, line => line.ComponentType == expected);
        }

        [Fact]
        public void An_unknown_source_charge_kind_fails_closed()
        {
            var exception = Assert.Throws<BusinessException>(() => _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                charges: [new OfferCharge("TAX-1", (AirChargeKind)99, "XX", "Unknown", true)])));

            Assert.Equal(2785, exception.Code);
        }

        [Fact]
        public void An_unresolvable_source_charge_fails_closed_instead_of_becoming_a_fee()
        {
            var exception = Assert.Throws<BusinessException>(() => _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                charges: [])));

            Assert.Equal(2785, exception.Code);
        }

        [Fact]
        public void An_original_sale_is_normalized_to_customer_balance_debit_originals()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));

            Assert.All(source.PricingLines, line => Assert.Equal(PricingEffect.CustomerBalance, line.Effect));
            Assert.All(source.PricingLines, line => Assert.Equal(OrderPricingLineDirection.Debit, line.Direction));
            Assert.All(source.PricingLines, line => Assert.Equal(PricingLineRole.Original, line.LineRole));
        }

        [Fact]
        public void Source_and_sale_amounts_and_both_currencies_are_preserved()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines:
                [
                    AirPriceOfferFixture.FareLine(1_000_000m),
                    AirPriceOfferFixture.ConvertedChargeLine("TAX-1", "I6", 50m, 90_000m, "ROE-1")
                ],
                rates:
                [
                    new OfferRate("ROE-1", AirPriceOfferFixture.ForeignCurrencyId, AirPriceOfferFixture.CurrencyId, 1_800m, 4)
                ]));

            var tax = source.PricingLines.Single(line => line.ComponentType == PricingComponentType.Tax);

            Assert.Equal(50m, tax.OriginalAmount);
            Assert.Equal(AirPriceOfferFixture.ForeignCurrencyId, tax.OriginalCurrencyId);
            Assert.Equal(90_000m, tax.SaleAmount);
            Assert.Equal(AirPriceOfferFixture.CurrencyId, tax.SaleCurrencyId);
        }

        [Fact]
        public void Source_applied_conversion_provenance_is_preserved()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines:
                [
                    AirPriceOfferFixture.FareLine(1_000_000m),
                    AirPriceOfferFixture.ConvertedChargeLine("TAX-1", "I6", 50m, 90_000m, "ROE-1")
                ],
                rates:
                [
                    new OfferRate("ROE-1", AirPriceOfferFixture.ForeignCurrencyId, AirPriceOfferFixture.CurrencyId, 1_800m, 4)
                ]));

            var tax = source.PricingLines.Single(line => line.ComponentType == PricingComponentType.Tax);

            Assert.NotNull(tax.ExchangeRate);
            Assert.Equal(1_800m, tax.ExchangeRate!.RateOfExchange);
            Assert.Equal(4, tax.ExchangeRate.NumberOfDecimalPlaces);
            Assert.Equal("ROE-1", tax.ExchangeRate.RateOfExchangeId);
        }

        [Fact]
        public void A_repeated_source_code_keeps_one_identity_and_distinct_occurrences()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines:
                [
                    AirPriceOfferFixture.FareLine(1_000_000m),
                    AirPriceOfferFixture.ChargeLine("TAX-1", "I6", 90_000m),
                    AirPriceOfferFixture.ChargeLine("TAX-1", "I6", 12_000m)
                ]));

            var taxes = source.PricingLines.Where(line => line.ComponentType == PricingComponentType.Tax).ToList();

            Assert.Equal(2, taxes.Count);
            Assert.Single(taxes.Select(line => line.SourceLineRef).Distinct());
            Assert.Equal(["1", "2"], taxes.Select(line => line.OccurrenceKey).Order());
        }

        [Fact]
        public void Different_source_lines_keep_distinct_identities()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines:
                [
                    AirPriceOfferFixture.FareLine(1_000_000m),
                    AirPriceOfferFixture.ChargeLine("TAX-1", "I6", 90_000m),
                    AirPriceOfferFixture.ChargeLine("TAX-2", "OT", 5_000m)
                ],
                charges:
                [
                    new OfferCharge("TAX-1", AirChargeKind.Tax, "I6", "Value added tax", true),
                    new OfferCharge("TAX-2", AirChargeKind.Fee, "OT", "Booking fee", false)
                ]));

            Assert.Equal(3, source.PricingLines.Select(line => line.SourceLineRef).Distinct().Count());
        }

        [Fact]
        public void The_occurrence_is_not_embedded_in_the_source_reference()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));

            Assert.All(source.PricingLines, line => Assert.Equal(5, line.SourceLineRef.Split(':').Length));
            Assert.All(source.PricingLines, line => Assert.Equal("1", line.OccurrenceKey));
        }

        [Fact]
        public void Commercial_terms_evidence_is_preserved()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));
            var terms = source.Products.Single().CommercialTerms;

            Assert.True(terms.IsRefundable);
            Assert.True(terms.IsChangeable);
            Assert.False(terms.IsUpgradable);
            Assert.Equal("AirPrice", terms.PolicySource);
            Assert.Equal(AirPriceOfferFixture.AirFareId.ToString(), terms.SourceRuleReference);
        }

        [Fact]
        public void Baggage_evidence_is_preserved_with_its_unit()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));
            var terms = source.Products.Single().CommercialTerms;

            Assert.Equal(1, terms.CheckedBaggage!.Pieces);
            Assert.Equal(20m, terms.CheckedBaggage.Weight);
            Assert.Equal(WeightUnit.Kg, terms.CheckedBaggage.Unit);
            Assert.Equal(7m, terms.CabinBaggage!.Weight);
        }

        [Theory]
        [InlineData("STONE")]
        [InlineData("")]
        [InlineData(null)]
        public void An_unknown_baggage_unit_is_never_silently_converted_to_kilograms(string? unit)
        {
            var exception = Assert.Throws<BusinessException>(
                () => _normalizer.Normalize(AirPriceOfferFixture.Offer(Now, baggageUnit: unit)));

            Assert.Equal(2786, exception.Code);
        }

        [Fact]
        public void The_ticketing_deadline_is_normalized()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));

            Assert.Equal(Now.AddDays(1), source.TicketingDeadline);
        }

        [Fact]
        public void Marketing_and_operating_carriers_remain_distinct()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));
            var segment = source.Journeys.Single().Segments.Single();

            Assert.Equal(10, segment.MarketingAirlineId);
            Assert.Equal(20, segment.OperatingAirlineId);
            Assert.NotEqual(segment.MarketingAirlineId, segment.OperatingAirlineId);
        }

        [Fact]
        public void Traveller_journey_segment_and_service_correlations_are_stable()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));

            var journey = source.Journeys.Single();
            var segment = journey.Segments.Single();
            var product = source.Products.Single();
            var service = product.Services.Single();

            Assert.Equal("B1", journey.JourneyRef);
            Assert.Equal(BoundDirection.Outbound, journey.Direction);
            Assert.Equal("B1:5001", segment.SegmentRef);
            Assert.Equal("T1", product.TravellerRef);
            Assert.Equal("T1:B1:5001", service.ServiceRef);
            Assert.Equal(segment.SegmentRef, service.SegmentRef);
            Assert.All(source.PricingLines, line => Assert.Equal(product.ProductRef, line.ProductRef));
            Assert.All(source.PricingLines, line => Assert.Equal(service.ServiceRef, line.ServiceRef));
            Assert.Contains(source.Travellers, traveller => traveller.TravellerRef == "T1" && traveller.TravellerIndex == 1);
        }

        [Fact]
        public void The_product_snapshot_carries_the_accepted_source_evidence()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(Now));
            var snapshot = source.Products.Single().Snapshot;

            Assert.Equal(ProductType.AirFare, snapshot.ProductType);
            Assert.Equal(AirPriceOfferFixture.AirFareId.ToString(), snapshot.SourceProductReference);
            Assert.Equal("YOW", snapshot.ProductName);
            Assert.Equal("ECO", snapshot.Brand);
            Assert.Equal("AirPrice", snapshot.SourceSystem);
            Assert.Equal(AirPriceOfferFixture.OfferId, snapshot.SourceOfferId);
            Assert.Equal(10, snapshot.MarketingAirlineId);
            Assert.Equal(20, snapshot.OperatingAirlineId);
        }

        [Fact]
        public void An_order_level_charge_is_normalized_to_an_order_basis_line()
        {
            var source = _normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                charges:
                [
                    new OfferCharge("TAX-1", AirChargeKind.Tax, "I6", "Value added tax", true),
                    new OfferCharge("FEE-1", AirChargeKind.Fee, "OB", "Order fee", false)
                ],
                orderCharges: [AirPriceOfferFixture.ChargeLine("FEE-1", "OB", 15_000m)]));

            var orderFee = source.PricingLines.Single(line => line.BasisType == PricingBasisType.Order);

            Assert.Equal(PricingComponentType.Fee, orderFee.ComponentType);
            Assert.Equal(PricingApplicationLevel.PerOrder, orderFee.ApplicationLevel);
            Assert.Null(orderFee.ServiceRef);
        }
    }
}
