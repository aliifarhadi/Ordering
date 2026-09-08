using System.Reflection;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class CommercialSemanticsTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        // ---- product semantics ---------------------------------------------------

        [Fact]
        public void The_source_fare_identifier_stays_an_opaque_provenance_reference()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var snapshot = order.Items.Single().ProductSnapshot;

            Assert.Equal(OrderFactory.AirFareId.ToString(), snapshot.SourceProductReference);
            Assert.NotEqual(snapshot.SourceProductReference, snapshot.ProductCode);
            Assert.NotEqual(OrderFactory.AirFareId.ToString(), order.Items.Single().ProductCode);
        }

        [Fact]
        public void A_fare_basis_is_never_persisted_as_a_product_name()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.Items, item =>
            {
                Assert.NotEqual(MultiPassengerOrderFactory.FareBasis, item.ProductName);
                Assert.NotEqual(MultiPassengerOrderFactory.FareBasis, item.ProductSnapshot.ProductName);
            });
        }

        [Fact]
        public void An_absent_product_code_and_name_stay_absent_rather_than_being_synthesized()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            var item = order.Items.Single();

            Assert.Null(item.ProductCode);
            Assert.Null(item.ProductName);
            Assert.Null(item.ProductSnapshot.ProductCode);
            Assert.Null(item.ProductSnapshot.ProductName);
        }

        [Fact]
        public void A_brand_label_is_persisted_only_when_the_accepted_source_supplies_one()
        {
            var withBrand = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Equal("ECO", withBrand.Items.Single().ProductSnapshot.BrandName);
            Assert.Null(withBrand.Items.Single().ProductSnapshot.BrandCode);

            var source = OrderFactory.AcceptedSource(_clock);
            var unbranded = source with
            {
                Products = [source.Products.Single() with
                {
                    Snapshot = source.Products.Single().Snapshot with { BrandName = null }
                }]
            };

            var order = Order.Create(OrderFactory.Args(), unbranded, OrderFactory.OwnerAirlineId, _ids, _clock);

            Assert.Null(order.Items.Single().ProductSnapshot.BrandName);
        }

        // ---- commercial term summaries -------------------------------------------

        [Theory]
        [InlineData(CommercialTermState.Permitted)]
        [InlineData(CommercialTermState.Prohibited)]
        [InlineData(CommercialTermState.Conditional)]
        [InlineData(CommercialTermState.Unknown)]
        public void Any_ordering_commercial_term_state_is_accepted_without_a_schema_change(CommercialTermState state)
        {
            var source = OrderFactory.AcceptedSource(_clock);
            var product = source.Products.Single();

            var adjusted = source with
            {
                Products = [product with
                {
                    CommercialTerms = product.CommercialTerms with
                    {
                        ChangeabilitySummary = state,
                        SourcePolicyVersion = "RULES-2026-09"
                    }
                }]
            };

            var order = Order.Create(OrderFactory.Args(), adjusted, OrderFactory.OwnerAirlineId, _ids, _clock);
            var terms = order.Items.Single().CommercialTermsSnapshot;

            Assert.Equal(state, terms.ChangeabilitySummary);
            Assert.Equal("RULES-2026-09", terms.SourcePolicyVersion);
        }

        [Fact]
        public void A_conditional_summary_does_not_grant_the_legacy_permission_flag()
        {
            var source = OrderFactory.AcceptedSource(_clock);
            var product = source.Products.Single();

            var conditional = source with
            {
                Products = [product with
                {
                    CommercialTerms = product.CommercialTerms with
                    {
                        RefundabilitySummary = CommercialTermState.Conditional,
                        ChangeabilitySummary = CommercialTermState.Conditional
                    }
                }]
            };

            var order = Order.Create(OrderFactory.Args(), conditional, OrderFactory.OwnerAirlineId, _ids, _clock);

            Assert.Equal(CommercialTermState.Conditional, order.Items.Single().CommercialTermsSnapshot.RefundabilitySummary);
            Assert.Equal(CommercialTermState.Conditional, order.Items.Single().CommercialTermsSnapshot.ChangeabilitySummary);
        }

        [Fact]
        public void The_commercial_term_summaries_are_immutable_after_sale()
        {
            var type = typeof(OrderItemCommercialTermsSnapshot);

            foreach (var name in new[] { "RefundabilitySummary", "ChangeabilitySummary", "UpgradeEligibilitySummary" })
            {
                var property = type.GetProperty(name);

                Assert.NotNull(property);
                Assert.True(property!.SetMethod is null || !property.SetMethod.IsPublic);
            }

            Assert.Empty(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName));
        }

        [Fact]
        public void The_commercial_summary_cannot_itself_authorize_servicing()
        {
            var eligibilityPolicies = typeof(Order).Assembly.GetTypes()
                .Where(type => type.Namespace?.EndsWith(".OrderAggregate.Policies", StringComparison.Ordinal) == true)
                .ToList();

            Assert.NotEmpty(eligibilityPolicies);

            var offending = eligibilityPolicies
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(method => method.GetParameters()
                    .Any(parameter => parameter.ParameterType == typeof(OrderItemCommercialTermsSnapshot)))
                .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
                .ToList();

            Assert.Empty(offending);
        }

        // ---- ownership boundaries -------------------------------------------------

        [Fact]
        public void Baggage_is_not_duplicated_into_the_commercial_terms_snapshot()
        {
            var names = typeof(OrderItemCommercialTermsSnapshot)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToList();

            Assert.DoesNotContain(names, name => name.Contains("Baggage", StringComparison.OrdinalIgnoreCase));

            var acceptedNames = typeof(AcceptedCommercialTerms)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToList();

            Assert.DoesNotContain(acceptedNames, name => name.Contains("Baggage", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void The_accepted_air_service_carries_no_commercial_policy()
        {
            var names = typeof(AcceptedAirTransportDetail)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToList();

            Assert.DoesNotContain("IsRefundable", names);
            Assert.DoesNotContain("IsChangeable", names);
            Assert.DoesNotContain("IsUpgradable", names);
        }

        [Fact]
        public void The_air_service_and_its_detail_own_no_commercial_policy()
        {
            foreach (var type in new[] { typeof(OrderService), typeof(OrderAirTransportServiceDetail) })
            {
                var names = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(property => property.Name)
                    .ToList();

                Assert.DoesNotContain("IsRefundable", names);
                Assert.DoesNotContain("IsChangeable", names);
                Assert.DoesNotContain("IsUpgradable", names);
                Assert.DoesNotContain(names, name => name.Contains("FareFamily", StringComparison.Ordinal));
            }
        }
    }
}
