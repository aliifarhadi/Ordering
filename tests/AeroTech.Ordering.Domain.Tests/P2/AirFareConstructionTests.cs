using System.Reflection;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.P2
{
    public sealed class AirFareConstructionTests
    {
        private readonly SequentialIdGenerator _ids = SequentialIdGenerator.Unique();
        private readonly TestClock _clock = new();

        // ---- optionality ---------------------------------------------------------

        [Fact]
        public void An_order_without_fare_construction_is_valid()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.Empty(order.FareConstructions);
            Assert.Empty(order.CurrentFareConstructions());
            Assert.True(order.CustomerTotal > 0m);
            Assert.Single(order.PriceChangeSets);
        }

        [Fact]
        public void The_current_airprice_source_fabricates_no_fare_construction()
        {
            var source = MultiPassengerOrderFactory.AcceptedSource(_clock);

            Assert.True(source.FareConstructions is null || source.FareConstructions.Count == 0);
        }

        // ---- explicit structures --------------------------------------------------

        [Fact]
        public void An_explicit_one_way_construction_persists_as_accepted()
        {
            var order = WithConstruction(FareConstructionFactory.TwoOneWays());
            var construction = Assert.Single(order.FareConstructions);

            Assert.Equal(AirFareConstructionType.RoundTripFromOneWays, construction.ConstructionType);

            var units = construction.PricingGroups.Single().PricingUnits.OrderBy(unit => unit.Sequence).ToList();

            Assert.Equal(2, units.Count);
            Assert.All(units, unit => Assert.Equal(FarePricingUnitType.OneWay, unit.PricingUnitType));
            Assert.All(units, unit => Assert.Single(unit.FareComponents));
        }

        [Fact]
        public void An_explicit_true_round_trip_construction_persists_as_accepted()
        {
            var order = WithConstruction(FareConstructionFactory.TrueRoundTrip());
            var construction = Assert.Single(order.FareConstructions);

            Assert.Equal(AirFareConstructionType.RoundTrip, construction.ConstructionType);

            var unit = Assert.Single(construction.PricingGroups.Single().PricingUnits);

            Assert.Equal(FarePricingUnitType.RoundTrip, unit.PricingUnitType);
            Assert.Equal(FareCombinationMethod.FiledFare, unit.CombinationMethod);
            Assert.Equal(2, unit.FareComponents.Count);
        }

        [Fact]
        public void The_same_itinerary_priced_as_two_one_ways_stays_structurally_distinct_from_a_true_round_trip()
        {
            var roundTrip = WithConstruction(FareConstructionFactory.TrueRoundTrip());
            var oneWays = WithConstruction(FareConstructionFactory.TwoOneWays());

            var roundTripUnits = roundTrip.FareConstructions.Single().PricingGroups.Single().PricingUnits;
            var oneWayUnits = oneWays.FareConstructions.Single().PricingGroups.Single().PricingUnits;

            Assert.Single(roundTripUnits);
            Assert.Equal(2, oneWayUnits.Count);

            Assert.Equal(2, roundTripUnits.Single().FareComponents.Count);
            Assert.All(oneWayUnits, unit => Assert.Single(unit.FareComponents));

            Assert.Equal(
                roundTrip.FareConstructions.Single().FareComponents.Count(),
                oneWays.FareConstructions.Single().FareComponents.Count());
        }

        [Fact]
        public void An_open_jaw_construction_is_stored_when_the_source_declares_it()
        {
            var accepted = FareConstructionFactory.TwoOneWays() with
            {
                ConstructionType = AirFareConstructionType.OpenJaw
            };

            var order = WithConstruction(accepted);

            Assert.Equal(AirFareConstructionType.OpenJaw, order.FareConstructions.Single().ConstructionType);
        }

        // ---- nothing is inferred ---------------------------------------------------

        [Fact]
        public void A_construction_type_is_never_inferred_from_the_itinerary()
        {
            var order = WithConstruction(FareConstructionFactory.Unspecified());

            Assert.Null(order.FareConstructions.Single().ConstructionType);
        }

        [Fact]
        public void A_pricing_unit_type_and_combination_method_are_never_inferred()
        {
            var order = WithConstruction(FareConstructionFactory.Unspecified());
            var unit = order.FareConstructions.Single().PricingGroups.Single().PricingUnits.Single();

            Assert.Null(unit.PricingUnitType);
            Assert.Null(unit.CombinationMethod);
        }

        // ---- pricing groups ---------------------------------------------------------

        [Fact]
        public void A_pricing_group_defaults_to_one_traveller()
        {
            var order = WithConstruction(FareConstructionFactory.TrueRoundTrip("T1"), FareConstructionFactory.TrueRoundTrip("T2"));

            Assert.Equal(2, order.FareConstructions.Count);
            Assert.All(order.FareConstructions, construction =>
                Assert.All(construction.PricingGroups, group => Assert.Single(group.Travellers)));
        }

        [Fact]
        public void Two_travellers_sharing_a_passenger_type_are_not_merged_into_one_pricing_group()
        {
            var order = WithConstruction(FareConstructionFactory.TrueRoundTrip("T1"), FareConstructionFactory.TrueRoundTrip("T2"));

            var groups = order.FareConstructions.SelectMany(construction => construction.PricingGroups).ToList();

            Assert.Equal(2, groups.Count);
            Assert.All(groups, group => Assert.Equal(PassengerTypeCode.ADT, group.PassengerType));
            Assert.Equal(2, groups.SelectMany(group => group.Travellers).Select(t => t.OrderTravellerId).Distinct().Count());
        }

        [Fact]
        public void Travellers_explicitly_grouped_by_the_source_share_one_pricing_group()
        {
            var order = WithConstruction(FareConstructionFactory.SharedGroup());
            var group = order.FareConstructions.Single().PricingGroups.Single();

            Assert.Equal(2, group.Travellers.Count);
            Assert.Equal("GRP-1", group.SourceReference);
        }

        // ---- fare components ---------------------------------------------------------

        [Fact]
        public void A_through_fare_component_covers_several_sold_services_and_segments()
        {
            var order = WithConstruction(FareConstructionFactory.ThroughFare());
            var component = Assert.Single(order.FareConstructions.Single().FareComponents);

            Assert.Equal(2, component.Services.Count);
            Assert.Equal(2, component.Segments.Count);
            Assert.Equal("YTHRU", component.FareBasis);
        }

        [Fact]
        public void A_fare_break_produces_separate_components_with_separate_service_scopes()
        {
            var order = WithConstruction(FareConstructionFactory.TrueRoundTrip());
            var components = order.FareConstructions.Single().FareComponents.ToList();

            Assert.Equal(2, components.Count);
            Assert.All(components, component => Assert.Single(component.Services));

            var covered = components.SelectMany(component => component.Services).Select(service => service.OrderServiceId).ToList();

            Assert.Equal(covered.Count, covered.Distinct().Count());
        }

        [Fact]
        public void A_fare_component_is_not_an_order_service()
        {
            var order = WithConstruction(FareConstructionFactory.ThroughFare());
            var component = order.FareConstructions.Single().FareComponents.Single();

            Assert.IsType<OrderFareComponent>(component);
            Assert.DoesNotContain(order.OrderServices, service => service.Id == component.Id);
            Assert.NotEqual(order.OrderServices.Count, order.FareConstructions.Single().FareComponents.Count());
        }

        // ---- fare basis and brand -------------------------------------------------------

        [Fact]
        public void A_fare_basis_is_stored_as_opaque_context_and_is_never_parsed()
        {
            var order = WithConstruction(FareConstructionFactory.ThroughFare());
            var component = order.FareConstructions.Single().FareComponents.Single();

            Assert.Equal("YTHRU", component.FareBasis);

            var names = typeof(OrderFareComponent)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .ToList();

            Assert.DoesNotContain("IsRefundable", names);
            Assert.DoesNotContain("IsChangeable", names);
            Assert.DoesNotContain("IsUpgradable", names);
            Assert.DoesNotContain(names, name => name.Contains("Season", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, name => name.Contains("Baggage", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void A_brand_persists_independently_of_the_fare_basis()
        {
            var order = WithConstruction(FareConstructionFactory.ThroughFare());
            var component = order.FareConstructions.Single().FareComponents.Single();

            Assert.Equal("ECOFLEX", component.BrandCode);
            Assert.Equal("Economy Flex", component.BrandName);
            Assert.NotEqual(component.FareBasis, component.BrandName);
        }

        [Fact]
        public void No_fare_family_model_is_introduced()
        {
            var offending = typeof(Order).Assembly.GetTypes()
                .Where(type => type.Name.Contains("FareFamily", StringComparison.Ordinal))
                .Select(type => type.FullName!)
                .ToList();

            Assert.Empty(offending);
        }

        // ---- independence from the monetary ledger ------------------------------------

        [Fact]
        public void A_fare_component_requires_no_matching_pricing_line()
        {
            var order = WithConstruction(FareConstructionFactory.ThroughFare());

            Assert.Single(order.FareConstructions.Single().FareComponents);
            Assert.True(order.PricingLines.Count > 1);
        }

        [Fact]
        public void Accepting_a_fare_construction_does_not_move_monetary_state()
        {
            var withoutConstruction = MultiPassengerOrderFactory.Create(_ids, _clock);
            var withConstruction = WithConstruction(FareConstructionFactory.TrueRoundTrip());

            Assert.Equal(withoutConstruction.CustomerTotal, withConstruction.CustomerTotal);
            Assert.Equal(withoutConstruction.FinancialSequence, withConstruction.FinancialSequence);
            Assert.Equal(withoutConstruction.ObligationVersion, withConstruction.ObligationVersion);
            Assert.Equal(withoutConstruction.CommercialVersion, withConstruction.CommercialVersion);
            Assert.Single(withConstruction.PriceChangeSets);
            Assert.Single(withConstruction.Changes);
        }

        // ---- provenance ------------------------------------------------------------------

        [Fact]
        public void Supplied_source_references_are_retained_opaquely()
        {
            var order = WithConstruction(FareConstructionFactory.ThroughFare());
            var component = order.FareConstructions.Single().FareComponents.Single();

            Assert.Equal("TAR-1", component.TariffReference);
            Assert.Equal("RULE-1", component.RuleReference);
            Assert.Equal("ROUTE-1", component.RoutingReference);
            Assert.Equal("SRC-FARE-1", component.SourceFareReference);
        }

        [Fact]
        public void Missing_source_references_stay_null()
        {
            var order = WithConstruction(FareConstructionFactory.Unspecified());
            var construction = order.FareConstructions.Single();
            var component = construction.FareComponents.Single();

            Assert.Null(construction.SourcePricingReference);
            Assert.Null(component.TariffReference);
            Assert.Null(component.RuleReference);
            Assert.Null(component.RoutingReference);
            Assert.Null(component.SourceComponentReference);
            Assert.Null(component.BrandCode);
        }

        // ---- immutability and lineage ------------------------------------------------------

        [Fact]
        public void A_historical_construction_cannot_be_mutated()
        {
            foreach (var type in new[]
                     {
                         typeof(OrderAirFareConstruction), typeof(OrderFarePricingGroup),
                         typeof(OrderFarePricingUnit), typeof(OrderFareComponent)
                     })
            {
                Assert.All(type.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
                    Assert.True(property.SetMethod is null || !property.SetMethod.IsPublic,
                        $"{type.Name}.{property.Name} exposes a public setter."));

                Assert.Empty(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(method => !method.IsSpecialName && method.Name != nameof(OrderFareComponent.Covers)));
            }
        }

        [Fact]
        public void A_successor_construction_preserves_lineage_and_becomes_the_current_one()
        {
            var order = WithConstruction(FareConstructionFactory.TrueRoundTrip());
            var original = order.FareConstructions.Single();

            _clock.Advance(TimeSpan.FromMinutes(5));

            var successor = new OrderAirFareConstruction(new Domain.OrderAggregate.Arguments.CreateOrderAirFareConstructionArgs(
                _ids.NewId(),
                order.Id,
                order.Changes.Single().Id,
                FareConstructionFactory.SourceSystem,
                _clock.GetDateTime(),
                AirFareConstructionType.RoundTrip,
                SupersedesConstructionId: original.Id));

            Assert.Equal(original.Id, successor.SupersedesConstructionId);
            Assert.NotEqual(successor.Id, successor.SupersedesConstructionId);
        }

        [Fact]
        public void A_construction_cannot_supersede_itself()
        {
            var id = _ids.NewId();

            Assert.Throws<BusinessException>(() => new OrderAirFareConstruction(
                new Domain.OrderAggregate.Arguments.CreateOrderAirFareConstructionArgs(
                    id,
                    1,
                    1,
                    FareConstructionFactory.SourceSystem,
                    _clock.GetDateTime(),
                    SupersedesConstructionId: id)));
        }

        // ---- cross-order integrity -----------------------------------------------------------

        [Fact]
        public void A_fare_component_referencing_a_service_outside_the_order_is_refused()
        {
            var accepted = FareConstructionFactory.TrueRoundTrip();
            var group = accepted.PricingGroups.Single();
            var unit = group.PricingUnits.Single();

            var broken = accepted with
            {
                PricingGroups =
                [
                    group with
                    {
                        PricingUnits =
                        [
                            unit with
                            {
                                FareComponents =
                                [
                                    unit.FareComponents[0] with { ServiceRefs = ["GHOST"] }
                                ]
                            }
                        ]
                    }
                ]
            };

            var exception = Assert.Throws<BusinessException>(() => WithConstruction(broken));

            Assert.Equal(20122, exception.Code);
        }

        [Fact]
        public void A_pricing_group_referencing_a_traveller_outside_the_order_is_refused()
        {
            var accepted = FareConstructionFactory.TrueRoundTrip();

            var broken = accepted with
            {
                PricingGroups = [accepted.PricingGroups.Single() with { TravellerRefs = ["GHOST"] }]
            };

            var exception = Assert.Throws<BusinessException>(() => WithConstruction(broken));

            Assert.Equal(20122, exception.Code);
        }

        [Fact]
        public void A_construction_referencing_a_product_outside_the_order_is_refused()
        {
            var broken = FareConstructionFactory.TrueRoundTrip() with { ProductRefs = ["GHOST"] };

            var exception = Assert.Throws<BusinessException>(() => WithConstruction(broken));

            Assert.Equal(20122, exception.Code);
        }

        [Fact]
        public void A_fare_component_must_cover_at_least_one_sold_service()
        {
            var accepted = FareConstructionFactory.TrueRoundTrip();
            var group = accepted.PricingGroups.Single();
            var unit = group.PricingUnits.Single();

            var broken = accepted with
            {
                PricingGroups =
                [
                    group with
                    {
                        PricingUnits = [unit with { FareComponents = [unit.FareComponents[0] with { ServiceRefs = [] }] }]
                    }
                ]
            };

            var exception = Assert.Throws<BusinessException>(() => WithConstruction(broken));

            Assert.Equal(20131, exception.Code);
        }

        // ---- issue-time fare context ----------------------------------------------------------

        [Fact]
        public void Issue_time_fare_context_uses_the_fare_component_when_a_construction_exists()
        {
            var order = WithConstruction(FareConstructionFactory.ThroughFare());
            var services = order.OrderServices.Where(service => service.IsAirTransport)
                .Where(service => order.ActiveFareComponentFor(service.Id) is not null)
                .ToList();

            Assert.NotEmpty(services);
            Assert.All(services, service => Assert.Equal("YTHRU", order.ResolveIssueFareBasis(service.Id)));
            Assert.All(services, service => Assert.NotEqual(service.AirTransportDetail?.TransitionalFareBasis, order.ResolveIssueFareBasis(service.Id)));
        }

        [Fact]
        public void Issue_time_fare_context_falls_back_to_the_transitional_service_field_without_a_construction()
        {
            var order = MultiPassengerOrderFactory.Create(_ids, _clock);

            Assert.All(order.OrderServices.Where(service => service.IsAirTransport), service =>
            {
                Assert.Null(order.ActiveFareComponentFor(service.Id));
                Assert.Equal(service.AirTransportDetail?.TransitionalFareBasis, order.ResolveIssueFareBasis(service.Id));
            });
        }

        private Order WithConstruction(params AcceptedFareConstruction[] constructions)
        {
            var source = MultiPassengerOrderFactory.AcceptedSource(_clock) with
            {
                FareConstructions = constructions
            };

            return Order.Create(
                MultiPassengerOrderFactory.Args(),
                source,
                MultiPassengerOrderFactory.OwnerAirlineId,
                _ids,
                _clock);
        }
    }
}
