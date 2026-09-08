using System.Reflection;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class AcceptedSourceCreationTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        [Fact]
        public void Domain_creation_accepts_only_the_normalized_ordering_source()
        {
            var create = typeof(Order)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == nameof(Order.Create));

            Assert.Contains(create.GetParameters(), parameter => parameter.ParameterType == typeof(AcceptedOrderSource));
            Assert.DoesNotContain(create.GetParameters(), parameter =>
                parameter.ParameterType.FullName!.Contains("Offer", StringComparison.Ordinal));
        }

        [Fact]
        public void The_customer_total_equals_the_accepted_source_sale_values()
        {
            var source = OrderFactory.AcceptedSource(_clock);
            var order = Order.Create(OrderFactory.Args(), source, OrderFactory.OwnerAirlineId, _ids, _clock);

            Assert.Equal(source.PricingLines.Sum(line => line.SaleAmount), order.CustomerTotal);
            Assert.Equal(order.CustomerTotal, order.Amount.GrandTotal);
        }

        [Fact]
        public void Every_order_item_carries_a_product_snapshot()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.NotEmpty(order.Items);
            Assert.All(order.Items, item =>
            {
                Assert.NotNull(item.ProductSnapshot);
                Assert.Equal(ProductType.AirFare, item.ProductSnapshot.ProductType);
                Assert.Equal(MultiPassengerOrderFactory.SourceOfferId, item.ProductSnapshot.SourceOfferId);
                Assert.Equal(MultiPassengerOrderFactory.SourceSystem, item.ProductSnapshot.SourceSystem);
                Assert.False(string.IsNullOrWhiteSpace(item.ProductSnapshot.SourceProductReference));
            });
        }

        [Fact]
        public void Every_order_item_carries_a_commercial_terms_snapshot()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.Items, item =>
            {
                Assert.NotNull(item.CommercialTermsSnapshot);
                Assert.Equal(CommercialTermState.Permitted, item.CommercialTermsSnapshot.RefundabilitySummary);
                Assert.Equal(CommercialTermState.Permitted, item.CommercialTermsSnapshot.ChangeabilitySummary);
                Assert.Equal(CommercialTermState.Prohibited, item.CommercialTermsSnapshot.UpgradeEligibilitySummary);
                Assert.Equal(MultiPassengerOrderFactory.SourceSystem, item.CommercialTermsSnapshot.SourceSystem);
            });
        }

        [Fact]
        public void The_snapshots_are_immutable_accepted_sale_evidence()
        {
            AssertNoPublicMutators(typeof(Domain.OrderAggregate.Entities.OrderItemProductSnapshot));
            AssertNoPublicMutators(typeof(Domain.OrderAggregate.Entities.OrderItemCommercialTermsSnapshot));
        }

        [Fact]
        public void A_later_source_refresh_cannot_change_the_accepted_snapshot()
        {
            var source = OrderFactory.AcceptedSource(_clock);
            var order = Order.Create(OrderFactory.Args(), source, OrderFactory.OwnerAirlineId, _ids, _clock);

            var snapshot = order.Items.Single().ProductSnapshot;
            var terms = order.Items.Single().CommercialTermsSnapshot;

            var brandBefore = snapshot.BrandName;
            var refundabilityBefore = terms.RefundabilitySummary;

            var refreshed = source.Products.Single() with
            {
                Snapshot = source.Products.Single().Snapshot with { BrandName = "CHANGED" },
                CommercialTerms = source.Products.Single().CommercialTerms with
                {
                    RefundabilitySummary = CommercialTermState.Prohibited
                }
            };

            Assert.Equal("CHANGED", refreshed.Snapshot.BrandName);
            Assert.Equal(CommercialTermState.Prohibited, refreshed.CommercialTerms.RefundabilitySummary);

            Assert.Equal(brandBefore, order.Items.Single().ProductSnapshot.BrandName);
            Assert.Equal(refundabilityBefore, order.Items.Single().CommercialTermsSnapshot.RefundabilitySummary);
        }

        [Fact]
        public void Source_line_identity_and_occurrence_survive_creation()
        {
            var source = OrderFactory.AcceptedSource(_clock);
            var order = Order.Create(OrderFactory.Args(), source, OrderFactory.OwnerAirlineId, _ids, _clock);

            foreach (var accepted in source.PricingLines)
                Assert.Contains(order.PricingLines, line =>
                    line.SourceLineRef == accepted.SourceLineRef && line.OccurrenceKey == accepted.OccurrenceKey);
        }

        [Fact]
        public void An_initial_sale_commits_exactly_one_original_sale_change_set()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            var changeSet = Assert.Single(order.PriceChangeSets);

            Assert.Equal(PriceChangeReason.OriginalSale, changeSet.Reason);
            Assert.Equal(PricingSource.OfferProvider, changeSet.Source);
            Assert.Equal(OrderFactory.SourceOfferId, changeSet.SourceOfferId);
            Assert.True(changeSet.IsCommitted);
            Assert.Equal(1, order.FinancialSequence);
            Assert.Equal(1, changeSet.FinancialSequence);
        }

        [Fact]
        public void An_initial_sale_records_one_create_change_and_the_expected_versions()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            var change = Assert.Single(order.Changes);

            Assert.Equal(OrderChangeType.Create, change.ChangeType);
            Assert.Equal(PricingSource.OfferProvider, change.Source);
            Assert.Equal(1, order.CommercialVersion);
            Assert.Equal(2, order.ObligationVersion);
        }

        [Fact]
        public void The_ticketing_deadline_becomes_an_ordering_time_to_live()
        {
            var source = OrderFactory.AcceptedSource(_clock);
            var order = Order.Create(OrderFactory.Args(), source, OrderFactory.OwnerAirlineId, _ids, _clock);

            Assert.Equal(source.TicketingDeadline, order.TimeToLive);
        }

        [Fact]
        public void Marketing_and_operating_carrier_roles_are_preserved_and_are_not_the_owner_airline()
        {
            var source = OrderFactory.AcceptedSource(_clock) with { };
            var order = Order.Create(OrderFactory.Args(), source, OrderFactory.OwnerAirlineId, _ids, _clock);

            var segment = order.Segments.Single();
            var accepted = source.Journeys.Single().Segments.Single();

            Assert.Equal(accepted.MarketingAirlineId, segment.MarketingAirlineId);
            Assert.Equal(accepted.OperatingAirlineId, segment.OperatingAirlineId);
            Assert.NotEqual(OrderFactory.OwnerAirlineId, segment.MarketingAirlineId);
            Assert.Equal(OrderFactory.OwnerAirlineId, order.OwnerAirlineId);
        }

        [Fact]
        public void An_accepted_source_without_products_is_refused()
        {
            var source = OrderFactory.AcceptedSource(_clock) with { Products = [] };

            var exception = Assert.Throws<Framework.Core.Domain.Exceptions.BusinessException>(
                () => Order.Create(OrderFactory.Args(), source, OrderFactory.OwnerAirlineId, _ids, _clock));

            Assert.Equal(2780, exception.Code);
        }

        [Fact]
        public void An_unresolvable_service_reference_is_refused()
        {
            var source = OrderFactory.AcceptedSource(_clock);
            var broken = source with
            {
                PricingLines = source.PricingLines.Select(line => line with { ServiceRef = "GHOST" }).ToList()
            };

            var exception = Assert.Throws<Framework.Core.Domain.Exceptions.BusinessException>(
                () => Order.Create(OrderFactory.Args(), broken, OrderFactory.OwnerAirlineId, _ids, _clock));

            Assert.Equal(2784, exception.Code);
        }

        private static void AssertNoPublicMutators(Type type)
        {
            Assert.All(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
                Assert.True(
                    property.SetMethod is null || !property.SetMethod.IsPublic,
                    $"{type.Name}.{property.Name} exposes a public setter."));

            Assert.Empty(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName));
        }
    }
}
