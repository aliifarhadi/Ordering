using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class AddProductPricingTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_direct_service_charge_increases_the_total_once()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.CustomerTotal;

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order, 200_000m)), _ids, _clock);

            Assert.Equal(before + 200_000m, order.CustomerTotal);

            var lines = order.PricingLines.Where(line => line.PriceChangeSetId == added.PriceChangeSetId).ToList();

            Assert.Single(lines);
            Assert.Equal(PricingComponentType.ProductCharge, lines[0].ComponentType);
            Assert.Equal(PricingBasisType.OrderService, lines[0].BasisType);
            Assert.Equal(added.OrderServiceIds.Single(), lines[0].BasisReferenceId);
            Assert.Equal(added.OrderItemId, lines[0].OrderItemId);
        }

        [Fact]
        public void An_item_priced_bundle_of_two_services_increases_the_total_once()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.CustomerTotal;

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order, 500_000m)),
                _ids,
                _clock);

            Assert.Equal(before + 500_000m, order.CustomerTotal);
            Assert.Single(order.PricingLines.Where(line => line.PriceChangeSetId == added.PriceChangeSetId));
            Assert.All(added.OrderServiceIds, id => Assert.Equal(
                ServicePriceTreatment.Included,
                order.OrderServices.Single(service => service.Id == id).PriceTreatment));
        }

        [Fact]
        public void A_tax_line_does_not_duplicate_service_value()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.CustomerTotal;

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.SeatWithTaxAndCommission(order, 200_000m, 20_000m, 10_000m)),
                _ids,
                _clock);

            Assert.Equal(before + 220_000m, order.CustomerTotal);

            var serviceId = added.OrderServiceIds.Single();
            var attributions = order.ServiceValueAttributions(serviceId);

            Assert.Equal(2, attributions.Count);
            Assert.Equal(220_000m, attributions.Sum(attribution => attribution.SignedSaleAmount));
        }

        [Fact]
        public void Commission_stays_settlement_only_and_does_not_reduce_the_customer_total()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.CustomerTotal;

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.SeatWithTaxAndCommission(order, 200_000m, 20_000m, 10_000m)),
                _ids,
                _clock);

            var commission = order.PricingLines
                .Single(line => line.PriceChangeSetId == added.PriceChangeSetId
                                && line.ComponentType == PricingComponentType.Commission);

            Assert.Equal(PricingEffect.SettlementOnly, commission.Effect);
            Assert.False(commission.AffectsCustomerBalance);
            Assert.Equal(before + 220_000m, order.CustomerTotal);
            Assert.Equal(10_000m, order.Commission.CommissionAmount);
        }

        [Fact]
        public void A_source_supplied_discount_reduces_the_accepted_addition_total()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.CustomerTotal;
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

            Assert.Equal(before + 150_000m, order.CustomerTotal);
        }

        [Fact]
        public void A_reversal_line_is_rejected_in_an_addition()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order);

            var reversing = accepted with
            {
                PricingLines =
                [
                    .. accepted.PricingLines,
                    ProductAdditionFactory.Line(
                        PricingComponentType.Fare,
                        1_000m,
                        PricingBasisType.OrderItem,
                        direction: OrderPricingLineDirection.Credit,
                        lineRole: PricingLineRole.Reversal)
                ]
            };

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(reversing), _ids, _clock));

            Assert.Equal(2853, exception.Code);
        }

        [Fact]
        public void Source_occurrence_rules_remain_enforced()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order);

            var duplicated = accepted with
            {
                PricingLines = [accepted.PricingLines[0], accepted.PricingLines[0]]
            };

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(duplicated), _ids, _clock));

            Assert.Equal(2776, exception.Code);
        }

        [Fact]
        public void An_invalid_second_pricing_line_leaves_the_whole_addition_unchanged()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order);
            var snapshot = Snapshot(order);

            var broken = accepted with
            {
                PricingLines =
                [
                    .. accepted.PricingLines,
                    ProductAdditionFactory.Line(PricingComponentType.Tax, 10_000m, PricingBasisType.Journey)
                ]
            };

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(broken), _ids, _clock));

            Assert.Equal(2857, exception.Code);
            AssertUnchanged(order, snapshot);
        }

        [Fact]
        public void An_invalid_allocation_leaves_the_whole_addition_unchanged()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var snapshot = Snapshot(order);
            var air = ProductAdditionFactory.OutboundAirService(order);

            var service = ProductAdditionFactory.Service(
                "SEAT-1",
                OrderServiceType.SeatAssignment,
                "SEAT",
                new AcceptedAddedSeatDetail(air.Id, "14C"),
                [air.SoleBeneficiaryId],
                coveredOrderServiceIds: [air.Id]);

            var addition = ProductAdditionFactory.Addition(
                ProductAdditionFactory.Product(ProductType.Seat, [service]),
                [
                    ProductAdditionFactory.Line(
                        PricingComponentType.ProductCharge,
                        200_000m,
                        PricingBasisType.OrderService,
                        "SEAT-1",
                        allocationSets:
                        [
                            new AcceptedAdditionAllocationSet(
                                PricingAllocationPurpose.CommercialValue,
                                PricingSource.PricingEngine,
                                PricingAllocationMethod.SourceProvided,
                                PricingAllocationCompleteness.Complete,
                                [new AcceptedAdditionAllocation(120_000m, ProductAdditionFactory.CurrencyId, "SEAT-1")])
                        ])
                ]);

            Assert.ThrowsAny<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(addition), _ids, _clock));

            AssertUnchanged(order, snapshot);
        }

        [Fact]
        public void A_source_supplied_complete_allocation_is_preserved()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var outbound = ProductAdditionFactory.OutboundAirService(order);
            var inbound = ProductAdditionFactory.InboundAirService(order);
            var bundle = ProductAdditionFactory.RoundTripBaggageBundle(order, 500_000m);

            var allocated = bundle with
            {
                PricingLines =
                [
                    ProductAdditionFactory.Line(
                        PricingComponentType.ProductCharge,
                        500_000m,
                        PricingBasisType.OrderItem,
                        allocationSets:
                        [
                            new AcceptedAdditionAllocationSet(
                                PricingAllocationPurpose.CommercialValue,
                                PricingSource.PricingEngine,
                                PricingAllocationMethod.SourceProvided,
                                PricingAllocationCompleteness.Complete,
                                [
                                    new AcceptedAdditionAllocation(300_000m, ProductAdditionFactory.CurrencyId, "BAG-OUT", outbound.SoleBeneficiaryId),
                                    new AcceptedAdditionAllocation(200_000m, ProductAdditionFactory.CurrencyId, "BAG-IN", inbound.SoleBeneficiaryId)
                                ])
                        ])
                ]
            };

            var added = order.AddProduct(ProductAdditionFactory.Args(allocated), _ids, _clock);

            var line = order.PricingLines.Single(candidate => candidate.PriceChangeSetId == added.PriceChangeSetId);
            var allocations = line.CommercialAllocations().ToList();

            Assert.Equal(2, allocations.Count);
            Assert.Equal(500_000m, allocations.Sum(allocation => allocation.SaleAmount));
            Assert.Equal(added.OrderServiceIds.Order(), allocations.Select(allocation => allocation.OrderServiceId!.Value).Order());
        }

        [Fact]
        public void No_allocation_is_invented_for_a_bundle_price()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order, 500_000m)),
                _ids,
                _clock);

            var line = order.PricingLines.Single(candidate => candidate.PriceChangeSetId == added.PriceChangeSetId);

            Assert.Empty(line.AllocationSets);
            Assert.All(added.OrderServiceIds, id => Assert.Empty(order.ServiceValueAttributions(id)));
        }

        [Fact]
        public void A_separately_priced_service_without_primary_value_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order);

            var taxOnly = accepted with
            {
                PricingLines =
                [
                    ProductAdditionFactory.Line(PricingComponentType.Tax, 20_000m, PricingBasisType.OrderService, "SEAT-1", code: "I6")
                ]
            };

            var exception = Assert.Throws<BusinessException>(
                () => order.AddProduct(ProductAdditionFactory.Args(taxOnly), _ids, _clock));

            Assert.Equal(2854, exception.Code);
        }

        [Fact]
        public void An_order_level_fee_creates_no_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.CustomerTotal;
            var accepted = ProductAdditionFactory.Seat(order, 200_000m);

            var withOrderFee = accepted with
            {
                PricingLines =
                [
                    .. accepted.PricingLines,
                    ProductAdditionFactory.Line(PricingComponentType.Fee, 15_000m, PricingBasisType.Order, productRef: null, code: "OB")
                ]
            };

            var added = order.AddProduct(ProductAdditionFactory.Args(withOrderFee), _ids, _clock);

            Assert.Single(added.OrderServiceIds);
            Assert.Equal(before + 215_000m, order.CustomerTotal);
            Assert.DoesNotContain(order.OrderServices, service => service.ServiceType == OrderServiceType.ServiceFee);

            var fee = order.PricingLines.Single(line => line.ComponentType == PricingComponentType.Fee);

            Assert.Equal(order.Id, fee.BasisReferenceId);
            Assert.Null(fee.OrderItemId);
        }

        [Fact]
        public void An_addition_without_pricing_evidence_is_rejected()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var accepted = ProductAdditionFactory.Seat(order);

            var exception = Assert.Throws<BusinessException>(() => order.AddProduct(
                ProductAdditionFactory.Args(accepted with { PricingLines = [] }),
                _ids,
                _clock));

            Assert.Equal(2768, exception.Code);
        }

        private static (int Items, int Services, int Lines, int Sets, int Links, int Changes, decimal Total, int Commercial, long Financial, long Obligation) Snapshot(Order order)
            => (order.Items.Count, order.OrderServices.Count, order.PricingLines.Count, order.PriceChangeSets.Count,
                order.ItemServiceLinks.Count, order.Changes.Count, order.CustomerTotal, order.CommercialVersion,
                order.FinancialSequence, order.ObligationVersion);

        private static void AssertUnchanged(
            Order order,
            (int Items, int Services, int Lines, int Sets, int Links, int Changes, decimal Total, int Commercial, long Financial, long Obligation) snapshot)
            => Assert.Equal(snapshot, Snapshot(order));
    }
}
