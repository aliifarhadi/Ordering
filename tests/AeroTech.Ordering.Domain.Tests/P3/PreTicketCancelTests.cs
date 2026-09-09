using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P3
{
    public sealed class PreTicketCancelTests
    {
        private const int Currency = MultiPassengerOrderFactory.CurrencyId;

        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_pre_ticket_cancellation_reverses_the_whole_effective_position()
        {
            var order = Confirmed();
            var totalBefore = order.CustomerTotal;

            Assert.True(totalBefore > 0m);

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(0m, order.CustomerTotal);
            Assert.All(order.OrderServices, service => Assert.Equal(OrderServiceStatus.Cancelled, service.Status));
        }

        [Fact]
        public void The_cancellation_reversal_is_stamped_ordering_derived()
        {
            var order = Confirmed();

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            var cancellation = CancellationSet(order);

            Assert.Equal(PricingSource.OrderingDerived, cancellation.Source);
            Assert.Equal(PriceChangeReason.Cancellation, cancellation.Reason);
        }

        [Fact]
        public void The_reversal_is_not_derived_from_pricing_allocations()
        {
            var order = Confirmed();
            var serviceId = order.OrderServices.First().Id;

            order.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.AddProduct,
                    PriceChangeReason.AddProduct,
                    PricingSource.PricingEngine,
                    [
                        StandaloneLine(400_000m) with
                        {
                            AllocationSets =
                            [
                                new AcceptedPricingAllocationSetArgs(
                                    PricingAllocationPurpose.CommercialValue,
                                    PricingSource.OfferProvider,
                                    PricingAllocationMethod.SourceProvided,
                                    PricingAllocationCompleteness.Partial,
                                    [new AcceptedPricingAllocationArgs(1_000m, Currency, OrderServiceId: serviceId)])
                            ]
                        }
                    ]),
                _ids,
                _clock);

            var misleading = order.PricingLines
                .Single(line => line.LineRole == PricingLineRole.Original && line.SaleAmount == 400_000m);

            Assert.Equal(1_000m, misleading.CommercialAllocations().Sum(allocation => allocation.SaleAmount));

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            var reversal = order.PricingLines.Single(line =>
                line.LineRole == PricingLineRole.Reversal && line.OriginalPricingLineId == misleading.Id);

            Assert.Equal(400_000m, reversal.SaleAmount);
            Assert.NotEqual(1_000m, reversal.SaleAmount);
            Assert.Equal(0m, order.CustomerTotal);
        }

        [Fact]
        public void An_order_changed_after_creation_is_reversed_once_at_its_current_position()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var createdTotal = order.CustomerTotal;

            order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.SeatWithTaxAndCommission(order, 200_000m, 20_000m, 10_000m)),
                _ids,
                _clock);

            Assert.Equal(createdTotal + 220_000m, order.CustomerTotal);

            order.CompleteReserve("ABC123", _clock.GetDateTime().AddDays(1), [], _ids, _clock);

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            Assert.Equal(0m, order.CustomerTotal);

            foreach (var original in order.PricingLines.Where(line => line.LineRole == PricingLineRole.Original))
                Assert.Equal(original.SaleAmount, order.ReversedSaleAmount(original.Id));

            var reversalsPerOriginal = order.PricingLines
                .Where(line => line.LineRole == PricingLineRole.Reversal)
                .GroupBy(line => line.OriginalPricingLineId);

            Assert.All(reversalsPerOriginal, group => Assert.Single(group));
        }

        [Fact]
        public void An_already_reversed_line_is_not_reversed_again()
        {
            var order = Confirmed();

            var fare = order.PricingLines.First(line =>
                line.LineRole == PricingLineRole.Original && line.ComponentType == PricingComponentType.Fare);

            order.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.ManualAdjustment,
                    PriceChangeReason.Correction,
                    PricingSource.Manual,
                    [ReversalOf(fare)]),
                _ids,
                _clock);

            Assert.Equal(fare.SaleAmount, order.ReversedSaleAmount(fare.Id));

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            var cancellation = CancellationSet(order);

            Assert.DoesNotContain(
                order.PricingLines.Where(line => line.PriceChangeSetId == cancellation.Id),
                line => line.OriginalPricingLineId == fare.Id);

            Assert.Equal(fare.SaleAmount, order.ReversedSaleAmount(fare.Id));
            Assert.Equal(0m, order.CustomerTotal);
        }

        [Fact]
        public void Cancellation_advances_each_version_exactly_once()
        {
            var order = Confirmed();

            var commercialBefore = order.CommercialVersion;
            var financialBefore = order.FinancialSequence;
            var obligationBefore = order.ObligationVersion;

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            Assert.Equal(commercialBefore + 1, order.CommercialVersion);
            Assert.Equal(financialBefore + 1, order.FinancialSequence);
            Assert.Equal(obligationBefore + 1, order.ObligationVersion);
            Assert.Single(order.Changes, change => change.ChangeType == OrderChangeType.Cancel);
        }

        [Fact]
        public void Cancellation_emits_sibling_events_sharing_the_final_version()
        {
            var order = Confirmed();

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            var cancelled = Assert.Single(order.GetEvents().OfType<OrderCancelled>());
            var pricing = Assert.Single(
                order.GetEvents().OfType<OrderPricingChanged>(),
                candidate => candidate.Reason == PriceChangeReason.Cancellation);

            Assert.Equal(order.CommercialVersion, cancelled.CommercialVersion);
            Assert.Equal(order.CommercialVersion, pricing.CommercialVersion);
            Assert.Equal(1, cancelled.EventOrdinal);
            Assert.Equal(2, pricing.EventOrdinal);
            Assert.Equal(CancellationSet(order).Id, pricing.PriceChangeSetId);
        }

        [Fact]
        public void A_ticketed_order_cannot_use_the_pre_ticket_cancel_path()
        {
            var order = Confirmed();
            var serviceIds = order.OrderServices.Select(service => service.Id).ToList();

            order.RecordIssuedDocuments(
                serviceIds.Select(id => new IssuedServiceDocument(id, 900_000 + id, 800_000 + id)).ToList());
            order.CompleteTicketing(_clock);

            Assert.Equal(OrderStatus.Ticketed, order.Status);

            var financialBefore = order.FinancialSequence;

            Assert.Throws<BusinessException>(
                () => order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids));

            Assert.Equal(OrderStatus.Ticketed, order.Status);
            Assert.Equal(financialBefore, order.FinancialSequence);
            Assert.DoesNotContain(order.Changes, change => change.ChangeType == OrderChangeType.Cancel);
        }

        [Fact]
        public void A_documented_service_denies_the_pre_ticket_scope_even_when_the_status_allows_it()
        {
            var order = Confirmed();
            var documented = order.OrderServices.Take(1).Select(service => service.Id).ToList();

            var denied = WithdrawEligibilityPolicy.Evaluate(order, documented);
            var allowed = WithdrawEligibilityPolicy.Evaluate(order, []);

            Assert.False(denied.IsAllowed);
            Assert.Contains(EligibilityReasonCodes.AlreadyIssued, denied.ReasonCodes);
            Assert.True(allowed.IsAllowed);
        }

        private Order Confirmed()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            order.CompleteReserve("ABC123", _clock.GetDateTime().AddDays(1), [], _ids, _clock);
            return order;
        }

        private static OrderPriceChangeSet CancellationSet(Order order)
            => order.PriceChangeSets.Single(set => set.Reason == PriceChangeReason.Cancellation);

        private static AcceptedPricingLineArgs StandaloneLine(decimal amount)
            => new(
                PricingComponentType.ProductCharge,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                amount,
                Currency,
                amount,
                Currency,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                Code: "MISLEADING");

        private static AcceptedPricingLineArgs ReversalOf(OrderPricingLine original)
            => new(
                original.ComponentType,
                original.Effect,
                PricingComponentPolicy.Opposite(original.Direction),
                PricingLineRole.Reversal,
                original.OriginalAmount,
                original.OriginalCurrencyId,
                original.SaleAmount,
                original.SaleCurrencyId,
                original.BasisType,
                original.Refundability,
                ExchangeRate: original.ExchangeRate?.Copy(),
                OriginalPricingLineId: original.Id);
    }
}
