using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class OrderPricingChangedEventTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void Creating_an_order_emits_exactly_one_pricing_change_event()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var raised = Events(order);

            Assert.Single(raised);
            Assert.Equal(PriceChangeReason.OriginalSale, raised[0].Reason);
            Assert.Equal(1, raised[0].CommercialVersion);
            Assert.Equal(1, raised[0].FinancialSequence);
        }

        [Fact]
        public void Every_original_sale_line_travels_in_the_same_envelope()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var raised = Assert.Single(Events(order));
            var set = order.PriceChangeSets.Single(candidate => candidate.Id == raised.PriceChangeSetId);

            Assert.Equal(
                order.PricingLines.Count(line => line.PriceChangeSetId == set.Id),
                raised.PricingLines.Count);
            Assert.Contains(raised.PricingLines, line => line.ComponentType == PricingComponentType.Fare);
            Assert.Contains(raised.PricingLines, line => line.ComponentType == PricingComponentType.Tax);
        }

        [Fact]
        public void Adding_a_service_emits_one_further_pricing_change_event()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.SeatWithTaxAndCommission(order, 200_000m, 20_000m, 10_000m)),
                _ids,
                _clock);

            var raised = Events(order);

            Assert.Equal(2, raised.Count);

            var addition = raised.Single(candidate => candidate.Reason == PriceChangeReason.AddProduct);

            Assert.Equal(added.PriceChangeSetId, addition.PriceChangeSetId);
            Assert.Equal(added.OrderChangeId, addition.OrderChangeId);
            Assert.Equal(3, addition.PricingLines.Count);
            Assert.Equal(ProductAdditionFactory.OperationId, addition.OperationId);
        }

        [Fact]
        public void The_event_carries_the_final_commercial_version_not_the_expected_one()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);

            var addition = Events(order).Single(candidate => candidate.Reason == PriceChangeReason.AddProduct);
            var set = order.PriceChangeSets.Single(candidate => candidate.Id == added.PriceChangeSetId);

            Assert.Equal(2, addition.CommercialVersion);
            Assert.Equal(1, set.ExpectedCommercialVersion);
            Assert.Equal(order.CommercialVersion, addition.CommercialVersion);
            Assert.Equal(order.FinancialSequence, addition.FinancialSequence);
            Assert.Equal(order.ObligationVersion, addition.ObligationVersion);
        }

        [Fact]
        public void Sibling_events_share_the_commercial_version_and_use_distinct_ordinals()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);

            var productAdded = Assert.Single(order.GetEvents().OfType<OrderProductAdded>());
            var pricingChanged = Events(order).Single(candidate => candidate.Reason == PriceChangeReason.AddProduct);

            Assert.Equal(productAdded.CommercialVersion, pricingChanged.CommercialVersion);
            Assert.NotEqual(productAdded.EventOrdinal, pricingChanged.EventOrdinal);
            Assert.Equal(1, productAdded.EventOrdinal);
            Assert.Equal(2, pricingChanged.EventOrdinal);
        }

        [Fact]
        public void The_create_events_share_the_first_commercial_version()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var created = Assert.Single(order.GetEvents().OfType<OrderCreated>());
            var pricing = Assert.Single(Events(order));

            Assert.Equal(1, created.CommercialVersion);
            Assert.Equal(1, pricing.CommercialVersion);
            Assert.Equal(1, created.EventOrdinal);
            Assert.Equal(2, pricing.EventOrdinal);
        }

        [Fact]
        public void The_customer_balance_impact_and_total_are_derived_from_accepted_lines()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var totalBefore = order.CustomerTotal;

            order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.SeatWithTaxAndCommission(order, 200_000m, 20_000m, 10_000m)),
                _ids,
                _clock);

            var addition = Events(order).Single(candidate => candidate.Reason == PriceChangeReason.AddProduct);

            Assert.Equal(220_000m, addition.CustomerBalanceImpact);
            Assert.Equal(totalBefore + 220_000m, addition.CustomerTotalAfter);
            Assert.Equal(order.CustomerTotal, addition.CustomerTotalAfter);
            Assert.Equal(order.CurrencyId, addition.CurrencyId);
        }

        [Fact]
        public void A_settlement_only_change_still_emits_an_event_with_no_balance_impact()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var obligationBefore = order.ObligationVersion;

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.SettlementOnly(order)), _ids, _clock);

            var addition = Events(order).Single(candidate => candidate.Reason == PriceChangeReason.AddProduct);

            Assert.Equal(0m, addition.CustomerBalanceImpact);
            Assert.Equal(2, addition.FinancialSequence);
            Assert.Equal(2, addition.CommercialVersion);
            Assert.Equal(obligationBefore, addition.ObligationVersion);
        }

        [Fact]
        public void Line_amounts_stay_non_negative_magnitudes_with_explicit_direction()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order, 200_000m);

            var discounted = accepted with
            {
                PricingLines =
                [
                    .. accepted.PricingLines,
                    ProductAdditionFactory.Line(
                        PricingComponentType.Discount,
                        50_000m,
                        PricingBasisType.OrderItem,
                        direction: OrderPricingLineDirection.Credit,
                        code: "PROMO")
                ]
            };

            order.AddProduct(ProductAdditionFactory.Args(discounted), _ids, _clock);

            var addition = Events(order).Single(candidate => candidate.Reason == PriceChangeReason.AddProduct);
            var discount = addition.PricingLines.Single(line => line.ComponentType == PricingComponentType.Discount);

            Assert.All(addition.PricingLines, line => Assert.True(line.SaleAmount >= 0m));
            Assert.All(addition.PricingLines, line => Assert.True(line.OriginalAmount >= 0m));
            Assert.Equal(OrderPricingLineDirection.Credit, discount.Direction);
            Assert.Equal(50_000m, discount.SaleAmount);
            Assert.Equal(150_000m, addition.CustomerBalanceImpact);
        }

        [Fact]
        public void Commission_stays_settlement_only_in_the_event()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.SeatWithTaxAndCommission(order)),
                _ids,
                _clock);

            var commission = Events(order)
                .Single(candidate => candidate.Reason == PriceChangeReason.AddProduct)
                .PricingLines
                .Single(line => line.ComponentType == PricingComponentType.Commission);

            Assert.Equal(PricingEffect.SettlementOnly, commission.Effect);
            Assert.Equal("AGENCY-1", commission.SettlementPartyRef);
            Assert.Equal("Commission", commission.SettlementCategory);
        }

        [Fact]
        public void Repeated_tax_occurrences_remain_distinguishable()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var pricing = Assert.Single(Events(order));
            var taxes = pricing.PricingLines.Where(line => line.ComponentType == PricingComponentType.Tax).ToList();

            Assert.Equal(4, taxes.Count);
            Assert.All(taxes, tax => Assert.Equal("I6", tax.Code));
            Assert.Equal(taxes.Count, taxes.Select(tax => (tax.SourceLineRef, tax.OccurrenceKey)).Distinct().Count());
        }

        [Fact]
        public void Source_references_are_never_fabricated()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var pricing = Assert.Single(Events(order));

            Assert.Equal(MultiPassengerOrderFactory.SourceOfferId, pricing.SourceOfferId);
            Assert.Null(pricing.SourcePricingReference);
            Assert.All(pricing.PricingLines, line => Assert.Null(line.OriginalPricingLineId));
        }

        [Fact]
        public void Allocations_appear_only_when_the_source_supplied_them()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order, 500_000m)),
                _ids,
                _clock);

            var addition = Events(order).Single(candidate => candidate.Reason == PriceChangeReason.AddProduct);

            Assert.All(addition.PricingLines, line => Assert.Empty(line.AllocationSets));
        }

        [Fact]
        public void The_event_carries_no_provider_payload_and_no_order_blob()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var pricing = Assert.Single(Events(order));

            var names = pricing.GetType()
                .GetProperties()
                .Select(property => property.Name)
                .ToList();

            Assert.DoesNotContain("Order", names);
            Assert.DoesNotContain("Travellers", names);
            Assert.DoesNotContain("Items", names);
            Assert.DoesNotContain("Services", names);
            Assert.DoesNotContain(names, name => name.Contains("Offer", StringComparison.Ordinal) && name != "SourceOfferId");
        }

        [Fact]
        public void Non_pricing_operations_emit_no_pricing_change_event()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var serviceIds = order.OrderServices.Select(service => service.Id).ToList();

            var before = Events(order).Count;

            order.ApplyReservationOutcome(serviceIds, "PNR-1", null, _ids, _clock);
            order.RecordIssuedDocuments(
                serviceIds.Select(id => new IssuedServiceDocument(id, 900_000 + id, 800_000 + id)).ToList());
            order.CompleteTicketing(_clock);

            Assert.Equal(before, Events(order).Count);
        }

        private static IReadOnlyList<OrderPricingChanged> Events(Order order)
            => order.GetEvents().OfType<OrderPricingChanged>().ToList();
    }
}
