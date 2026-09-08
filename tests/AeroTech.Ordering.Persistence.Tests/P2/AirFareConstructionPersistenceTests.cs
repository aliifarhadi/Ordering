using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class AirFareConstructionPersistenceTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public AirFareConstructionPersistenceTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_true_round_trip_construction_survives_a_reload_with_its_full_hierarchy()
        {
            await using var harness = NewHarness();
            var created = await harness.CreateOrderAsync(BuildOrder(harness, FareConstructionFactory.TrueRoundTrip()));

            await using var reload = _fixture.NewCommandContext();
            var order = await new Persistence.OrderAggregate.OrderRepository(reload).GetAsync(created.Id);

            Assert.NotNull(order);

            var construction = Assert.Single(order!.FareConstructions);

            Assert.Equal(AirFareConstructionType.RoundTrip, construction.ConstructionType);
            Assert.Equal(FareConstructionFactory.SourceSystem, construction.SourceSystem);
            Assert.Equal(2, construction.Items.Count);

            var group = Assert.Single(construction.PricingGroups);

            Assert.Single(group.Travellers);
            Assert.Equal(PassengerTypeCode.ADT, group.PassengerType);

            var unit = Assert.Single(group.PricingUnits);

            Assert.Equal(FarePricingUnitType.RoundTrip, unit.PricingUnitType);
            Assert.Equal(FareCombinationMethod.FiledFare, unit.CombinationMethod);
            Assert.Equal(2, unit.FareComponents.Count);
            Assert.All(unit.FareComponents, component => Assert.Single(component.Services));
        }

        [Fact]
        public async Task A_through_fare_component_covering_two_services_survives_a_reload()
        {
            await using var harness = NewHarness();
            var created = await harness.CreateOrderAsync(BuildOrder(harness, FareConstructionFactory.ThroughFare()));

            await using var reload = _fixture.NewCommandContext();
            var order = await new Persistence.OrderAggregate.OrderRepository(reload).GetAsync(created.Id);

            var component = Assert.Single(order!.FareConstructions.Single().FareComponents);

            Assert.Equal(2, component.Services.Count);
            Assert.Equal(2, component.Segments.Count);
            Assert.Equal("YTHRU", component.FareBasis);
            Assert.Equal("Economy Flex", component.BrandName);
            Assert.Equal("RULE-1", component.RuleReference);
        }

        [Fact]
        public async Task An_order_without_a_construction_persists_none()
        {
            await using var harness = NewHarness();
            var created = await harness.CreateOrderAsync();

            await using var verification = _fixture.NewCommandContext();

            Assert.False(await verification.Set<OrderAirFareConstruction>()
                .AsNoTracking()
                .AnyAsync(construction => construction.OrderId == created.Id));
        }

        [Fact]
        public async Task A_through_fare_component_does_not_duplicate_issued_coupons()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();
            var created = await harness.CreateOrderAsync(BuildOrder(harness, FareConstructionFactory.ThroughFare()));

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            var result = await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            Assert.Equal(ServicingOperationStatus.Completed, result.OperationStatus);

            await using var verification = _fixture.NewCommandContext();

            var tickets = await verification.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .AsNoTracking()
                .Where(ticket => ticket.CurrentServicingOrderId == created.Id)
                .ToListAsync();

            var couponServiceIds = tickets.SelectMany(ticket => ticket.Coupons).Select(coupon => coupon.OrderServiceId).ToList();

            Assert.Equal(created.OrderServices.Count, couponServiceIds.Count);
            Assert.Equal(couponServiceIds.Count, couponServiceIds.Distinct().Count());
        }

        [Fact]
        public async Task Issued_coupons_take_their_fare_context_from_the_fare_component()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();
            var created = await harness.CreateOrderAsync(BuildOrder(harness, FareConstructionFactory.ThroughFare()));

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            await using var verification = _fixture.NewCommandContext();

            var coupons = await verification.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .AsNoTracking()
                .Where(ticket => ticket.CurrentServicingOrderId == created.Id)
                .SelectMany(ticket => ticket.Coupons)
                .ToListAsync();

            var throughFareCoupons = coupons
                .Where(coupon => created.ActiveFareComponentFor(coupon.OrderServiceId) is not null)
                .ToList();

            Assert.NotEmpty(throughFareCoupons);
            Assert.All(throughFareCoupons, coupon => Assert.Equal("YTHRU", coupon.FareBasisSnapshot));
        }

        [Fact]
        public async Task Ticket_value_attribution_still_comes_from_pricing_lines_not_fare_components()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();
            var created = await harness.CreateOrderAsync(BuildOrder(harness, FareConstructionFactory.ThroughFare()));

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            await using var verification = _fixture.NewCommandContext();

            var links = await verification.Set<DocumentPriceLink>().AsNoTracking().ToListAsync();
            var pricingLineIds = created.PricingLines.Select(line => line.Id).ToHashSet();
            var fareComponentIds = created.FareConstructions.SelectMany(c => c.FareComponents).Select(c => c.Id).ToHashSet();

            var orderLinks = links.Where(link => pricingLineIds.Contains(link.PricingLineId)).ToList();

            Assert.NotEmpty(orderLinks);
            Assert.All(links, link => Assert.DoesNotContain(link.PricingLineId, fareComponentIds));
        }


        [Fact]
        public async Task Each_service_is_ticketed_with_the_fare_basis_of_its_own_construction()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var source = MultiPassengerOrderFactory.AcceptedSource(harness.Clock) with
            {
                FareConstructions =
                [
                    FareConstructionFactory.SingleService("A", "T1", "B1", MultiPassengerOrderFactory.OutboundFlightId, "YOUT"),
                    FareConstructionFactory.SingleService("B", "T1", "B2", MultiPassengerOrderFactory.InboundFlightId, "YIN")
                ]
            };

            var order = Order.Create(
                MultiPassengerOrderFactory.Args(),
                source,
                MultiPassengerOrderFactory.OwnerAirlineId,
                harness.Ids,
                harness.Clock);

            var created = await harness.CreateOrderAsync(order);

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            await using var verification = _fixture.NewCommandContext();

            var coupons = await verification.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .AsNoTracking()
                .Where(ticket => ticket.CurrentServicingOrderId == created.Id)
                .SelectMany(ticket => ticket.Coupons)
                .ToListAsync();

            foreach (var coupon in coupons)
            {
                var expected = created.ResolveIssueFareBasis(coupon.OrderServiceId);

                Assert.Equal(expected, coupon.FareBasisSnapshot);
            }

            Assert.Contains(coupons, coupon => coupon.FareBasisSnapshot == "YOUT");
            Assert.Contains(coupons, coupon => coupon.FareBasisSnapshot == "YIN");
        }

        private static Order BuildOrder(OrderSliceHarness harness, Domain.OrderAggregate.AcceptedSource.AcceptedFareConstruction construction)
        {
            var source = MultiPassengerOrderFactory.AcceptedSource(harness.Clock) with
            {
                FareConstructions = [construction]
            };

            return Order.Create(
                MultiPassengerOrderFactory.Args(),
                source,
                MultiPassengerOrderFactory.OwnerAirlineId,
                harness.Ids,
                harness.Clock);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");
    }
}
