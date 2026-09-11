using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class PricingCorrectnessTests
    {
        private const int Currency = OrderFactory.CurrencyId;
        private const int ForeignCurrency = OrderFactory.CurrencyId + 7;

        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        // ---- exception atomicity -------------------------------------------------

        [Fact]
        public void A_rejected_first_line_leaves_the_aggregate_exactly_unchanged()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var before = Snapshot(order);

            Assert.Throws<BusinessException>(() => Commit(order, Invalid()));

            AssertUnchanged(order, before);
        }

        [Fact]
        public void A_rejected_later_line_does_not_attach_the_earlier_valid_lines()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var before = Snapshot(order);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Fee(40m),
                Invalid()));

            Assert.DoesNotContain(order.PricingLines, line => line.ComponentType == PricingComponentType.Fee);
            AssertUnchanged(order, before);
        }

        [Fact]
        public void A_rejected_reversal_leaves_the_aggregate_exactly_unchanged()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var fare = FareLine(order);
            var before = Snapshot(order);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Reversal(fare, fare.SaleAmount + 1m, fare.OriginalAmount + 1m)));

            AssertUnchanged(order, before);
        }

        [Fact]
        public void An_accepted_change_after_a_rejected_one_takes_the_next_financial_sequence()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => Commit(order, Invalid()));

            Commit(order, Fee(10m));

            Assert.Equal(2, order.FinancialSequence);
            Assert.Equal([1, 2], order.PriceChangeSets.Select(set => set.FinancialSequence).Order());
        }

        // ---- reversal provenance -------------------------------------------------

        [Fact]
        public void A_full_reversal_copies_the_original_and_sale_magnitudes()
        {
            var order = ConvertedOrder(out var converted);
            var before = order.CustomerTotal;

            Commit(order, Reversal(converted, converted.SaleAmount, converted.OriginalAmount));

            var reversal = order.PricingLines.Single(line => line.LineRole == PricingLineRole.Reversal);

            Assert.Equal(converted.SaleAmount, reversal.SaleAmount);
            Assert.Equal(converted.OriginalAmount, reversal.OriginalAmount);
            Assert.Equal(converted.OriginalCurrencyId, reversal.OriginalCurrencyId);
            Assert.Equal(before - converted.SaleAmount, order.CustomerTotal);
            Assert.Equal(0m, order.OutstandingSaleAmount(converted.Id));
        }

        [Fact]
        public void A_full_reversal_preserves_the_historical_conversion_provenance()
        {
            var order = ConvertedOrder(out var converted);

            Commit(order, Reversal(converted, converted.SaleAmount, converted.OriginalAmount));

            var reversal = order.PricingLines.Single(line => line.LineRole == PricingLineRole.Reversal);

            Assert.NotNull(reversal.ExchangeRate);
            Assert.Equal(converted.ExchangeRate, reversal.ExchangeRate);
        }

        [Fact]
        public void A_reversal_may_not_replace_the_historical_rate_with_todays_rate()
        {
            var order = ConvertedOrder(out var converted);

            var todaysRate = new ExchangeRate(new ExchangeRateArgs(0.95m, 2, "ROE-TODAY", 0));

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Reversal(converted, converted.SaleAmount, converted.OriginalAmount) with { ExchangeRate = todaysRate }));
        }

        [Fact]
        public void A_reversal_cannot_silently_supply_a_zero_original_amount()
        {
            var order = ConvertedOrder(out var converted);

            Assert.Throws<BusinessException>(() => Commit(order, Reversal(converted, 30m, 0m)));
        }

        [Fact]
        public void A_partial_reversal_respects_both_outstanding_caps()
        {
            var order = ConvertedOrder(out var converted);

            Commit(order, Reversal(converted, 30m, 15m));

            Assert.Equal(converted.SaleAmount - 30m, order.OutstandingSaleAmount(converted.Id));

            Assert.Throws<BusinessException>(() => Commit(order, Reversal(converted, 10m, converted.OriginalAmount)));
            Assert.Throws<BusinessException>(() => Commit(order, Reversal(converted, converted.SaleAmount, 10m)));

            Assert.Equal(30m, order.ReversedSaleAmount(converted.Id));
        }

        [Fact]
        public void A_final_partial_reversal_must_close_the_outstanding_original_exactly()
        {
            var order = ConvertedOrder(out var converted);

            Commit(order, Reversal(converted, 30m, 15m));

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Reversal(converted, converted.SaleAmount - 30m, converted.OriginalAmount - 20m)));

            Commit(order, Reversal(converted, converted.SaleAmount - 30m, converted.OriginalAmount - 15m));

            Assert.Equal(0m, order.OutstandingSaleAmount(converted.Id));
        }

        [Fact]
        public void A_reversal_cannot_reverse_another_reversal()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var fare = FareLine(order);

            Commit(order, Reversal(fare, 30m, 30m));

            var reversal = order.PricingLines.Single(line => line.LineRole == PricingLineRole.Reversal);

            var exception = Assert.Throws<BusinessException>(() => Commit(order, new AcceptedPricingLineArgs(
                reversal.ComponentType,
                reversal.Effect,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Reversal,
                10m,
                reversal.OriginalCurrencyId,
                10m,
                reversal.SaleCurrencyId,
                reversal.BasisType,
                reversal.Refundability,
                OriginalPricingLineId: reversal.Id)));

            Assert.Equal(20112, exception.Code);
        }

        [Fact]
        public void Two_reversals_in_one_change_set_are_capped_jointly()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var fare = FareLine(order);
            var before = Snapshot(order);

            Assert.Throws<BusinessException>(() => Commit(
                order,
                Reversal(fare, fare.SaleAmount, fare.OriginalAmount),
                Reversal(fare, 1m, 1m)));

            AssertUnchanged(order, before);
        }

        // ---- source occurrence identity -----------------------------------------

        [Fact]
        public void The_same_source_occurrence_cannot_appear_twice_in_one_change_set()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var before = Snapshot(order);

            var exception = Assert.Throws<BusinessException>(() => Commit(
                order,
                Fee(10m) with { SourceLineRef = "OFFER-1:T1:B1:5001:I6", OccurrenceKey = "1" },
                Fee(10m) with { SourceLineRef = "OFFER-1:T1:B1:5001:I6", OccurrenceKey = "1" }));

            Assert.Equal(20116, exception.Code);
            AssertUnchanged(order, before);
        }

        [Fact]
        public void The_same_source_reference_with_different_occurrences_is_accepted()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Commit(
                order,
                Fee(10m) with { SourceLineRef = "OFFER-1:T1:B1:5001:I6", OccurrenceKey = "1" },
                Fee(12m) with { SourceLineRef = "OFFER-1:T1:B1:5001:I6", OccurrenceKey = "2" });

            var accepted = order.PricingLines
                .Where(line => line.SourceLineRef == "OFFER-1:T1:B1:5001:I6")
                .ToList();

            Assert.Equal(2, accepted.Count);
            Assert.Equal(["1", "2"], accepted.Select(line => line.OccurrenceKey).Order());
        }

        [Fact]
        public void A_later_change_set_may_reference_the_same_source_occurrence_again()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Commit(order, Fee(10m) with { SourceLineRef = "REPRICE-REF", OccurrenceKey = "1" });
            Commit(order, Fee(10m) with { SourceLineRef = "REPRICE-REF", OccurrenceKey = "1" });

            Assert.Equal(2, order.PricingLines.Count(line => line.SourceLineRef == "REPRICE-REF"));
            Assert.Equal(3, order.FinancialSequence);
        }

        // ---- commission ----------------------------------------------------------

        [Fact]
        public void A_caller_supplied_commission_rate_is_not_authoritative()
        {
            var order = Order.Create(
                OrderFactory.Args() with { CommissionRate = 9m },
                OrderFactory.AcceptedSource(_clock),
                OrderFactory.OwnerAirlineId,
                _ids,
                _clock);

            Assert.Equal(0m, order.Commission.CommissionRate);
            Assert.Equal(0m, order.Commission.CommissionAmount);
            Assert.DoesNotContain(order.PricingLines, line => line.ComponentType == PricingComponentType.Commission);
        }

        [Fact]
        public void An_accepted_commission_fact_establishes_the_commission_and_leaves_the_customer_total_alone()
        {
            var order = Order.Create(
                OrderFactory.Args() with { CommissionRate = 9m },
                OrderFactory.AcceptedSource(_clock),
                OrderFactory.OwnerAirlineId,
                _ids,
                _clock);

            var before = order.CustomerTotal;

            Commit(order, new AcceptedPricingLineArgs(
                PricingComponentType.Commission,
                PricingEffect.SettlementOnly,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                7_500m,
                Currency,
                7_500m,
                Currency,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                Code: "COMM",
                UnitPrice: 5m,
                SettlementPartyRef: "AGENCY-11",
                SettlementCategory: "Commission"));

            Assert.Equal(7_500m, order.Commission.CommissionAmount);
            Assert.Equal(5m, order.Commission.CommissionRate);
            Assert.Equal(before, order.CustomerTotal);
        }

        // ---- allocation ----------------------------------------------------------

        [Fact]
        public void A_partial_allocation_exposes_an_explicit_residual()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            Commit(order, Fee(100m) with
            {
                AllocationSets =
                [
                    new AcceptedPricingAllocationSetArgs(
                        PricingAllocationPurpose.CommercialValue,
                        PricingSource.OfferProvider,
                        PricingAllocationMethod.SourceProvided,
                        PricingAllocationCompleteness.Partial,
                        [new AcceptedPricingAllocationArgs(70m, Currency, OrderServiceId: serviceId)])
                ]
            });

            var set = order.PricingLines
                .Single(line => line.ComponentType == PricingComponentType.Fee)
                .ActiveAllocationSet(PricingAllocationPurpose.CommercialValue)!;

            Assert.Equal(30m, set.ResidualSaleAmount);
            Assert.Equal(Currency, set.ResidualSaleCurrencyId);
            Assert.False(set.IsFullyAttributed);
        }

        [Fact]
        public void A_complete_allocation_has_no_residual()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            Commit(order, Fee(100m) with
            {
                AllocationSets =
                [
                    new AcceptedPricingAllocationSetArgs(
                        PricingAllocationPurpose.CommercialValue,
                        PricingSource.OfferProvider,
                        PricingAllocationMethod.SourceProvided,
                        PricingAllocationCompleteness.Complete,
                        [new AcceptedPricingAllocationArgs(100m, Currency, OrderServiceId: serviceId)])
                ]
            });

            var set = order.PricingLines
                .Single(line => line.ComponentType == PricingComponentType.Fee)
                .ActiveAllocationSet(PricingAllocationPurpose.CommercialValue)!;

            Assert.Equal(0m, set.ResidualSaleAmount);
            Assert.True(set.IsFullyAttributed);
        }

        [Fact]
        public void An_unavailable_allocation_leaves_the_whole_parent_value_unresolved()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Commit(order, Fee(100m) with
            {
                AllocationSets =
                [
                    new AcceptedPricingAllocationSetArgs(
                        PricingAllocationPurpose.CommercialValue,
                        PricingSource.OfferProvider,
                        PricingAllocationMethod.SourceProvided,
                        PricingAllocationCompleteness.Unavailable,
                        [])
                ]
            });

            var set = order.PricingLines
                .Single(line => line.ComponentType == PricingComponentType.Fee)
                .ActiveAllocationSet(PricingAllocationPurpose.CommercialValue)!;

            Assert.Equal(100m, set.ResidualSaleAmount);
        }

        [Fact]
        public void Supplied_original_allocation_values_must_reconcile_to_the_parent_original_amount()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            Assert.Throws<BusinessException>(() => Commit(order, ForeignFee(100m, 50m) with
            {
                AllocationSets =
                [
                    OriginalAllocationSet(
                        PricingAllocationCompleteness.Complete,
                        (serviceId, 100m, 30m))
                ]
            }));

            Commit(order, ForeignFee(100m, 50m) with
            {
                AllocationSets =
                [
                    OriginalAllocationSet(
                        PricingAllocationCompleteness.Complete,
                        (serviceId, 100m, 50m))
                ]
            });

            var set = order.PricingLines
                .Single(line => line.ComponentType == PricingComponentType.Fee)
                .ActiveAllocationSet(PricingAllocationPurpose.CommercialValue)!;

            Assert.Equal(0m, set.ResidualOriginalAmount);
            Assert.Equal(ForeignCurrency, set.ResidualOriginalCurrencyId);
        }

        [Fact]
        public void A_partial_original_allocation_cannot_exceed_the_parent_original_magnitude()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            Assert.Throws<BusinessException>(() => Commit(order, ForeignFee(100m, 50m) with
            {
                AllocationSets =
                [
                    OriginalAllocationSet(
                        PricingAllocationCompleteness.Partial,
                        (serviceId, 70m, 60m))
                ]
            }));
        }

        [Fact]
        public void A_missing_source_original_allocation_is_not_invented()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            Commit(order, ForeignFee(100m, 50m) with
            {
                AllocationSets =
                [
                    new AcceptedPricingAllocationSetArgs(
                        PricingAllocationPurpose.CommercialValue,
                        PricingSource.OfferProvider,
                        PricingAllocationMethod.SourceProvided,
                        PricingAllocationCompleteness.Complete,
                        [new AcceptedPricingAllocationArgs(100m, Currency, OrderServiceId: serviceId)])
                ]
            });

            var set = order.PricingLines
                .Single(line => line.ComponentType == PricingComponentType.Fee)
                .ActiveAllocationSet(PricingAllocationPurpose.CommercialValue)!;

            Assert.Null(set.ResidualOriginalAmount);
            Assert.Null(set.ResidualOriginalCurrencyId);
        }

        [Fact]
        public void A_half_supplied_original_allocation_breakdown_is_refused()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var serviceId = order.OrderServices.First().Id;

            var exception = Assert.Throws<BusinessException>(() => Commit(order, ForeignFee(100m, 50m) with
            {
                AllocationSets =
                [
                    new AcceptedPricingAllocationSetArgs(
                        PricingAllocationPurpose.CommercialValue,
                        PricingSource.OfferProvider,
                        PricingAllocationMethod.SourceProvided,
                        PricingAllocationCompleteness.Complete,
                        [
                            new AcceptedPricingAllocationArgs(60m, Currency, OrderServiceId: serviceId, OriginalAmount: 30m, OriginalCurrencyId: ForeignCurrency),
                            new AcceptedPricingAllocationArgs(40m, Currency, OrderServiceId: serviceId)
                        ])
                ]
            }));

            Assert.Equal(20117, exception.Code);
        }

        // ---- helpers -------------------------------------------------------------

        private Order ConvertedOrder(out OrderPricingLine converted)
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Commit(order, ForeignFee(100m, 50m));

            converted = order.PricingLines.Single(line => line.ComponentType == PricingComponentType.Fee);

            return order;
        }

        private static OrderPricingLine FareLine(Order order)
            => order.PricingLines.First(line => line.ComponentType == PricingComponentType.Fare);

        private static AcceptedPricingLineArgs Fee(decimal saleAmount)
            => new(
                PricingComponentType.Fee,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                saleAmount,
                Currency,
                saleAmount,
                Currency,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                Code: "FEE");

        private static AcceptedPricingLineArgs ForeignFee(decimal saleAmount, decimal originalAmount)
            => new(
                PricingComponentType.Fee,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                originalAmount,
                ForeignCurrency,
                saleAmount,
                Currency,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                Code: "FEE",
                ExchangeRate: new ExchangeRate(new ExchangeRateArgs(0.9m, 2, "ROE-HISTORIC", 0)));

        private static AcceptedPricingLineArgs Invalid()
            => new(
                PricingComponentType.Tax,
                PricingEffect.SettlementOnly,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                10m,
                Currency,
                10m,
                Currency,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                Code: "TAX",
                SettlementPartyRef: "GOV",
                SettlementCategory: "Tax");

        private static AcceptedPricingLineArgs Reversal(
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
                SettlementPartyRef: original.SettlementPartyRef,
                SettlementCategory: original.SettlementCategory,
                OriginalPricingLineId: original.Id);

        private static AcceptedPricingAllocationSetArgs OriginalAllocationSet(
            PricingAllocationCompleteness completeness,
            params (long ServiceId, decimal SaleAmount, decimal OriginalAmount)[] allocations)
            => new(
                PricingAllocationPurpose.CommercialValue,
                PricingSource.OfferProvider,
                PricingAllocationMethod.SourceProvided,
                completeness,
                allocations
                    .Select(allocation => new AcceptedPricingAllocationArgs(
                        allocation.SaleAmount,
                        Currency,
                        OrderServiceId: allocation.ServiceId,
                        OriginalAmount: allocation.OriginalAmount,
                        OriginalCurrencyId: ForeignCurrency))
                    .ToList());

        private void Commit(Order order, params AcceptedPricingLineArgs[] lines)
            => order.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.AddProduct,
                    PriceChangeReason.AddProduct,
                    PricingSource.PricingEngine,
                    lines),
                _ids,
                _clock);

        private static AggregateSnapshot Snapshot(Order order)
            => new(
                order.Changes.Count,
                order.PriceChangeSets.Count,
                order.PricingLines.Count,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                order.Amount.GrandTotal,
                order.Commission.CommissionAmount);

        private static void AssertUnchanged(Order order, AggregateSnapshot before)
            => Assert.Equal(before, Snapshot(order));

        private sealed record AggregateSnapshot(
            int Changes,
            int PriceChangeSets,
            int PricingLines,
            long FinancialSequence,
            long ObligationVersion,
            decimal CustomerTotal,
            decimal CachedGrandTotal,
            decimal CommissionAmount);
    }
}
