using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class PricingFoundationTests
    {
        private const int Currency = OrderFactory.CurrencyId;

        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void An_accepted_sale_is_a_debit_and_the_customer_total_is_the_signed_sum()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.All(order.PricingLines, line => Assert.Equal(PricingLineRole.Original, line.LineRole));
            Assert.All(order.PricingLines, line => Assert.Equal(OrderPricingLineDirection.Debit, line.Direction));
            Assert.All(order.PricingLines, line => Assert.Equal(PricingEffect.CustomerBalance, line.Effect));

            var expected = order.PricingLines.Sum(line => line.SaleAmount);

            Assert.Equal(expected, order.CustomerTotal);
            Assert.True(order.CustomerTotal > 0m);
        }

        [Fact]
        public void The_persisted_total_is_a_cache_of_the_ledger()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Equal(order.CustomerTotal, order.Amount.GrandTotal);
            Assert.Equal(
                order.PricingLines.Where(line => line.ComponentType == PricingComponentType.Tax).Sum(line => line.SignedSaleAmount),
                order.Amount.TaxTotal);
        }

        [Fact]
        public void A_discount_reduces_the_customer_total_without_a_second_negative_sign()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var before = order.CustomerTotal;

            Commit(order, Line(PricingComponentType.Discount, PricingEffect.CustomerBalance, OrderPricingLineDirection.Credit, 45m));

            Assert.Equal(before - 45m, order.CustomerTotal);
        }

        [Fact]
        public void A_discount_cannot_be_posted_as_a_customer_debit()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Line(PricingComponentType.Discount, PricingEffect.CustomerBalance, OrderPricingLineDirection.Debit, 45m)));
        }

        [Fact]
        public void Settlement_only_commission_does_not_move_the_customer_total()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var before = order.CustomerTotal;

            Commit(order, Line(
                PricingComponentType.Commission,
                PricingEffect.SettlementOnly,
                OrderPricingLineDirection.Debit,
                20m,
                settlementPartyRef: "AGENCY-11",
                settlementCategory: "Commission"));

            Assert.Equal(before, order.CustomerTotal);
            Assert.Equal(20m, order.Commission.CommissionAmount);
        }

        [Fact]
        public void Commission_cannot_be_charged_to_the_customer_balance()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Line(PricingComponentType.Commission, PricingEffect.CustomerBalance, OrderPricingLineDirection.Debit, 20m)));
        }

        [Fact]
        public void A_tax_can_never_be_settlement_only()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Line(
                    PricingComponentType.Tax,
                    PricingEffect.SettlementOnly,
                    OrderPricingLineDirection.Debit,
                    10m,
                    settlementPartyRef: "GOV",
                    settlementCategory: "Tax")));
        }

        [Fact]
        public void An_informational_line_is_excluded_from_the_customer_total()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var before = order.CustomerTotal;

            Commit(order, Line(PricingComponentType.Tax, PricingEffect.Informational, OrderPricingLineDirection.Debit, 15m));

            Assert.Equal(before, order.CustomerTotal);
        }

        [Fact]
        public void A_settlement_line_requires_an_explicit_party_and_category()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Line(PricingComponentType.Fee, PricingEffect.SettlementOnly, OrderPricingLineDirection.Debit, 10m)));
        }

        [Fact]
        public void A_magnitude_is_never_negative()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Line(PricingComponentType.Fee, PricingEffect.CustomerBalance, OrderPricingLineDirection.Debit, -10m)));
        }

        [Fact]
        public void A_reversal_needs_the_line_it_reverses()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Line(PricingComponentType.Fare, PricingEffect.CustomerBalance, OrderPricingLineDirection.Credit, 10m)
                    with { LineRole = PricingLineRole.Reversal }));
        }

        [Fact]
        public void A_reversal_must_oppose_its_original()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var original = FareLine(order);

            Assert.Throws<BusinessException>(() => Commit(order, ReversalOf(original, original.SaleAmount) with
            {
                Direction = OrderPricingLineDirection.Debit
            }));
        }

        [Fact]
        public void A_partial_reversal_is_bounded_by_the_outstanding_value()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var original = FareLine(order);
            var before = order.CustomerTotal;

            Commit(order, ReversalOf(original, 30m));

            Assert.Equal(before - 30m, order.CustomerTotal);
            Assert.Equal(original.SaleAmount - 30m, order.OutstandingSaleAmount(original.Id));

            Assert.Throws<BusinessException>(() => Commit(order, ReversalOf(original, original.SaleAmount)));

            Assert.Equal(30m, order.ReversedSaleAmount(original.Id));
        }

        [Fact]
        public void Create_records_source_identity_and_occurrence_separately()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.All(order.PricingLines, line => Assert.False(string.IsNullOrWhiteSpace(line.SourceLineRef)));
            Assert.All(order.PricingLines, line => Assert.False(string.IsNullOrWhiteSpace(line.OccurrenceKey)));

            Assert.All(order.PricingLines, line => Assert.Equal(5, line.SourceLineRef!.Split(':').Length));
            Assert.All(order.PricingLines, line => Assert.Equal("1", line.OccurrenceKey));
        }

        [Fact]
        public void The_financial_sequence_advances_once_per_committed_change_set()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Equal(1, order.FinancialSequence);
            Assert.Single(order.PriceChangeSets);

            Commit(order, Line(PricingComponentType.Fee, PricingEffect.CustomerBalance, OrderPricingLineDirection.Debit, 10m));

            Assert.Equal(2, order.FinancialSequence);
            Assert.Equal([1, 2], order.PriceChangeSets.Select(set => set.FinancialSequence).Order());
        }

        [Fact]
        public void A_committed_change_set_is_immutable()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var changeSet = order.PriceChangeSets.Single();

            Assert.True(changeSet.IsCommitted);
            Assert.Equal(PriceChangeReason.OriginalSale, changeSet.Reason);
            Assert.All(order.PricingLines, line => Assert.Equal(changeSet.Id, line.PriceChangeSetId));
        }

        [Fact]
        public void An_empty_change_set_is_refused()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => order.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.AddProduct,
                    PriceChangeReason.AddProduct,
                    PricingSource.PricingEngine,
                    []),
                _ids,
                _clock));
        }

        [Fact]
        public void The_obligation_version_advances_only_when_the_customer_total_moves()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var before = order.ObligationVersion;

            Commit(order, Line(
                PricingComponentType.Fee,
                PricingEffect.SettlementOnly,
                OrderPricingLineDirection.Debit,
                10m,
                settlementPartyRef: "SUPPLIER",
                settlementCategory: "Fee"));

            Assert.Equal(before, order.ObligationVersion);

            Commit(order, Line(PricingComponentType.Fee, PricingEffect.CustomerBalance, OrderPricingLineDirection.Debit, 10m));

            Assert.Equal(before + 1, order.ObligationVersion);
        }

        [Fact]
        public void A_complete_allocation_set_must_reconcile_to_its_parent_line()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            Assert.Throws<BusinessException>(() => Commit(order, Line(
                PricingComponentType.ProductCharge,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                80m) with
            {
                AllocationSets = [AllocationSet(PricingAllocationCompleteness.Complete, (serviceId, 35m))]
            }));

            Commit(order, Line(
                PricingComponentType.ProductCharge,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                80m) with
            {
                AllocationSets = [AllocationSet(PricingAllocationCompleteness.Complete, (serviceId, 80m))]
            });

            var bundle = order.PricingLines.Single(line => line.ComponentType == PricingComponentType.ProductCharge);

            Assert.Single(bundle.CommercialAllocations());
        }

        [Fact]
        public void A_partial_allocation_set_may_not_exceed_its_parent_line()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            Assert.Throws<BusinessException>(() => Commit(order, Line(
                PricingComponentType.ProductCharge,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                50m) with
            {
                AllocationSets = [AllocationSet(PricingAllocationCompleteness.Partial, (serviceId, 60m))]
            }));
        }

        [Fact]
        public void An_unavailable_allocation_set_may_not_invent_shares()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            Assert.Throws<BusinessException>(() => Commit(order, Line(
                PricingComponentType.ProductCharge,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                50m) with
            {
                AllocationSets = [AllocationSet(PricingAllocationCompleteness.Unavailable, (serviceId, 25m))]
            }));
        }

        [Fact]
        public void A_customer_effective_line_must_use_the_order_sale_currency()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Line(PricingComponentType.Fee, PricingEffect.CustomerBalance, OrderPricingLineDirection.Debit, 10m)
                    with { SaleCurrencyId = Currency + 1 }));
        }

        [Fact]
        public void Allocations_are_never_added_to_the_customer_total()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;
            var before = order.CustomerTotal;

            Commit(order, Line(
                PricingComponentType.ProductCharge,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                80m) with
            {
                AllocationSets =
                [
                    AllocationSet(PricingAllocationCompleteness.Complete, (serviceId, 50m), (serviceId, 30m))
                ]
            });

            Assert.Equal(before + 80m, order.CustomerTotal);
        }

        private static OrderPricingLine FareLine(Order order)
            => order.PricingLines.First(line => line.ComponentType == PricingComponentType.Fare);

        private static AcceptedPricingLineArgs ReversalOf(OrderPricingLine original, decimal saleAmount)
            => ReversalOf(original, saleAmount, saleAmount);

        private static AcceptedPricingLineArgs ReversalOf(
            OrderPricingLine original,
            decimal saleAmount,
            decimal originalAmount)
            => new(
                original.ComponentType,
                original.Effect,
                OrderPricingLineDirection.Credit,
                PricingLineRole.Reversal,
                originalAmount,
                original.OriginalCurrencyId,
                saleAmount,
                original.SaleCurrencyId,
                original.BasisType,
                original.Refundability,
                ExchangeRate: original.ExchangeRate,
                OriginalPricingLineId: original.Id);

        private static AcceptedPricingLineArgs Line(
            PricingComponentType componentType,
            PricingEffect effect,
            OrderPricingLineDirection direction,
            decimal saleAmount,
            string? settlementPartyRef = null,
            string? settlementCategory = null)
            => new(
                componentType,
                effect,
                direction,
                PricingLineRole.Original,
                saleAmount,
                Currency,
                saleAmount,
                Currency,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                Code: componentType.ToString(),
                SettlementPartyRef: settlementPartyRef,
                SettlementCategory: settlementCategory);

        private static AcceptedPricingAllocationSetArgs AllocationSet(
            PricingAllocationCompleteness completeness,
            params (long ServiceId, decimal Amount)[] allocations)
            => new(
                PricingAllocationPurpose.CommercialValue,
                PricingSource.OfferProvider,
                PricingAllocationMethod.SourceProvided,
                completeness,
                allocations
                    .Select(allocation => new AcceptedPricingAllocationArgs(
                        allocation.Amount,
                        Currency,
                        OrderServiceId: allocation.ServiceId))
                    .ToList());

        private void Commit(Order order, AcceptedPricingLineArgs line)
            => order.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.AddProduct,
                    PriceChangeReason.AddProduct,
                    PricingSource.PricingEngine,
                    [line]),
                _ids,
                _clock);
    }
}
