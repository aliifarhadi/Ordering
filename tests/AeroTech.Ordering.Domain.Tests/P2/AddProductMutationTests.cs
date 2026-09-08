using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ProductAddition;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class AddProductMutationTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_seat_product_is_added_to_an_existing_order()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);

            var item = order.Items.Single(candidate => candidate.Id == added.OrderItemId);
            var service = order.OrderServices.Single(candidate => candidate.Id == added.OrderServiceIds.Single());

            Assert.Equal(ProductType.Seat, item.ProductType);
            Assert.Equal(OrderServiceType.SeatAssignment, service.ServiceType);
            Assert.Equal("14C", service.SeatDetail!.SoldSeatNumber);
            Assert.Equal(OrderServiceStatus.Active, service.Status);
        }

        [Fact]
        public void One_addition_creates_exactly_one_item()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.Items.Count;

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order)), _ids, _clock);

            Assert.Equal(before + 1, order.Items.Count);
        }

        [Fact]
        public void One_item_may_create_several_services()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order)),
                _ids,
                _clock);

            Assert.Equal(2, added.OrderServiceIds.Count);
            Assert.All(added.OrderServiceIds, id =>
                Assert.Equal(added.OrderItemId, order.OrderServices.Single(service => service.Id == id).OrderItemId));
        }

        [Fact]
        public void Every_new_service_is_linked_by_the_same_addition_change()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(
                ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order)),
                _ids,
                _clock);

            var links = order.ItemServiceLinks.Where(link => link.LinkedByChangeId == added.OrderChangeId).ToList();

            Assert.Equal(2, links.Count);
            Assert.All(links, link => Assert.Equal(added.OrderItemId, link.OrderItemId));
            Assert.Equal(added.OrderServiceIds.Order(), links.Select(link => link.OrderServiceId).Order());
        }

        [Fact]
        public void The_addition_creates_exactly_one_order_change()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order)), _ids, _clock);

            var changes = order.Changes.Where(change => change.ChangeType == OrderChangeType.AddProduct).ToList();

            Assert.Single(changes);
            Assert.Equal(added.OrderChangeId, changes[0].Id);
            Assert.Equal(ProductAdditionFactory.OperationId, changes[0].OperationId);
            Assert.Equal(7, changes[0].ActorId);
        }

        [Fact]
        public void The_addition_commits_exactly_one_price_change_set()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);

            var sets = order.PriceChangeSets.Where(set => set.Reason == PriceChangeReason.AddProduct).ToList();

            Assert.Single(sets);
            Assert.Equal(added.PriceChangeSetId, sets[0].Id);
            Assert.Equal(added.OrderChangeId, sets[0].ChangeId);
            Assert.True(sets[0].IsCommitted);
        }

        [Fact]
        public void The_commercial_version_advances_exactly_once()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.CommercialVersion;

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order)), _ids, _clock);

            Assert.Equal(before + 1, order.CommercialVersion);
        }

        [Fact]
        public void The_financial_sequence_advances_exactly_once()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.FinancialSequence;

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order)), _ids, _clock);

            Assert.Equal(before + 1, order.FinancialSequence);
            Assert.Equal(before + 1, added.FinancialSequence);
        }

        [Fact]
        public void A_customer_balance_change_advances_the_obligation_version_once()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.ObligationVersion;

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);

            Assert.Equal(before + 1, order.ObligationVersion);
        }

        [Fact]
        public void A_settlement_only_addition_does_not_advance_the_obligation_version()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var obligationBefore = order.ObligationVersion;
            var totalBefore = order.CustomerTotal;
            var financialBefore = order.FinancialSequence;
            var commercialBefore = order.CommercialVersion;

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.SettlementOnly(order)), _ids, _clock);

            Assert.Equal(obligationBefore, order.ObligationVersion);
            Assert.Equal(totalBefore, order.CustomerTotal);
            Assert.Equal(financialBefore + 1, order.FinancialSequence);
            Assert.Equal(commercialBefore + 1, order.CommercialVersion);
        }

        [Fact]
        public void A_rejected_addition_advances_no_version()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var commercial = order.CommercialVersion;
            var financial = order.FinancialSequence;
            var obligation = order.ObligationVersion;
            var total = order.CustomerTotal;

            Assert.Throws<BusinessException>(() => order.AddProduct(RejectedAddition(order), _ids, _clock));

            Assert.Equal(commercial, order.CommercialVersion);
            Assert.Equal(financial, order.FinancialSequence);
            Assert.Equal(obligation, order.ObligationVersion);
            Assert.Equal(total, order.CustomerTotal);
            Assert.Equal(total, order.Amount.GrandTotal);
        }

        [Fact]
        public void A_rejected_addition_attaches_no_item()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.Items.Count;

            Assert.Throws<BusinessException>(() => order.AddProduct(RejectedAddition(order), _ids, _clock));

            Assert.Equal(before, order.Items.Count);
        }

        [Fact]
        public void A_rejected_addition_attaches_no_service()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.OrderServices.Count;

            Assert.Throws<BusinessException>(() => order.AddProduct(RejectedAddition(order), _ids, _clock));

            Assert.Equal(before, order.OrderServices.Count);
            Assert.DoesNotContain(order.OrderServices, service => service.ServiceType == OrderServiceType.SeatAssignment);
        }

        [Fact]
        public void A_rejected_addition_attaches_no_pricing_line()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var before = order.PricingLines.Count;
            var sets = order.PriceChangeSets.Count;

            Assert.Throws<BusinessException>(() => order.AddProduct(RejectedAddition(order), _ids, _clock));

            Assert.Equal(before, order.PricingLines.Count);
            Assert.Equal(sets, order.PriceChangeSets.Count);
        }

        [Fact]
        public void A_rejected_addition_attaches_no_item_service_link_or_change()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var links = order.ItemServiceLinks.Count;
            var changes = order.Changes.Count;

            Assert.Throws<BusinessException>(() => order.AddProduct(RejectedAddition(order), _ids, _clock));

            Assert.Equal(links, order.ItemServiceLinks.Count);
            Assert.Equal(changes, order.Changes.Count);
            Assert.DoesNotContain(order.Changes, change => change.ChangeType == OrderChangeType.AddProduct);
        }

        [Fact]
        public void The_addition_raises_one_product_added_event()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            var added = order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.RoundTripBaggageBundle(order)), _ids, _clock);

            var raised = order.GetEvents().OfType<OrderProductAdded>().ToList();

            Assert.Single(raised);
            Assert.Equal(added.OrderChangeId, raised[0].OrderChangeId);
            Assert.Equal(added.OrderItemId, raised[0].OrderItemId);
            Assert.Equal(2, raised[0].OrderServiceIds.Count);
            Assert.Equal(order.CommercialVersion, raised[0].CommercialVersion);
            Assert.Equal(1, raised[0].EventOrdinal);
            Assert.Equal(order.CustomerTotal, raised[0].CustomerTotal);
        }

        [Fact]
        public void A_terminal_order_cannot_accept_a_product()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);
            var addition = ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order));

            order.WithdrawBeforeTicketing(
                order.OrderServices.Select(service => service.Id).ToList(),
                VoidReason.CustomerRequest,
                7,
                _ids,
                _clock);

            var exception = Assert.Throws<BusinessException>(() => order.AddProduct(addition, _ids, _clock));

            Assert.Equal(2850, exception.Code);
        }

        [Fact]
        public void A_second_addition_advances_the_versions_again()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            order.AddProduct(ProductAdditionFactory.Args(ProductAdditionFactory.Seat(order)), _ids, _clock);

            var second = ProductAdditionFactory.Args(
                ProductAdditionFactory.GroundTransport(order),
                ProductAdditionFactory.OperationId + 1);

            order.AddProduct(second, _ids, _clock);

            Assert.Equal(3, order.CommercialVersion);
            Assert.Equal(3, order.FinancialSequence);
            Assert.Equal(2, order.Changes.Count(change => change.ChangeType == OrderChangeType.AddProduct));
        }

        private static AeroTech.Ordering.Domain.OrderAggregate.Arguments.AcceptedAddServiceChangeArgs RejectedAddition(Order order)
        {
            var accepted = ProductAdditionFactory.Seat(order);

            return ProductAdditionFactory.Args(accepted with
            {
                PricingLines =
                [
                    .. accepted.PricingLines,
                    ProductAdditionFactory.Line(
                        PricingComponentType.Tax,
                        1_000m,
                        PricingBasisType.OrderService,
                        "NOT-A-SERVICE")
                ]
            });
        }
    }
}
