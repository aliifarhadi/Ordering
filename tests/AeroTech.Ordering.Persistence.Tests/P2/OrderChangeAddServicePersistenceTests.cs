using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OrderChangeAddServicePersistenceTests
    {
        private const string GroundOfferId = "QOFFER-GRD";

        private readonly OrderingDatabaseFixture _fixture;

        public OrderChangeAddServicePersistenceTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task The_added_item_and_its_snapshots_round_trip()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            var reloaded = await ReloadAsync(order.Id);
            var item = reloaded.Items.Single(candidate => candidate.Id == outcome.OrderItemId);

            Assert.Equal(ProductType.Seat, item.ProductType);
            Assert.Equal(1m, item.Quantity);
            Assert.Equal(OrderItemUnitOfMeasure.Each, item.UnitOfMeasure);
            Assert.Null(item.PolicySnapshot);

            Assert.Equal(ProductAdditionFactory.SourceSystem, item.ProductSnapshot.SourceSystem);
            Assert.Equal(ProductAdditionFactory.QuotedOfferId, item.ProductSnapshot.SourceOfferId);
            Assert.Equal($"SRC-{ProductAdditionFactory.ProductRef}", item.ProductSnapshot.SourceProductReference);

            Assert.Equal(CommercialTermState.Conditional, item.CommercialTermsSnapshot.RefundabilitySummary);
            Assert.Equal(CommercialTermState.Prohibited, item.CommercialTermsSnapshot.ChangeabilitySummary);
            Assert.Equal(ProductAdditionFactory.SourceSystem, item.CommercialTermsSnapshot.SourceSystem);
        }

        [Fact]
        public async Task The_selected_quote_provenance_persists()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            var reloaded = await ReloadAsync(order.Id);
            var change = reloaded.Changes.Single(candidate => candidate.Id == outcome.OrderChangeId);
            var set = reloaded.PriceChangeSets.Single(candidate => candidate.Id == outcome.PriceChangeSetId);

            Assert.Equal(ProductAdditionFactory.SelectedOfferItemId, change.ExternalReference);
            Assert.Equal(ProductAdditionFactory.QuotedOfferId, set.SourceOfferId);
            Assert.Null(set.SourcePricingRef);
            Assert.Equal(PricingSource.PricingEngine, set.Source);
        }

        [Fact]
        public async Task The_added_service_detail_beneficiaries_and_coverage_round_trip()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var air = ProductAdditionFactory.OutboundAirService(order);

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            var reloaded = await ReloadAsync(order.Id);
            var service = reloaded.OrderServices.Single(candidate => candidate.Id == outcome.ServiceIds.Single());

            Assert.Equal(OrderServiceType.SeatAssignment, service.ServiceType);
            Assert.Equal("14C", service.SeatDetail!.SoldSeatNumber);
            Assert.Equal(air.Id, service.SeatDetail!.AssociatedAirOrderServiceId);
            Assert.Equal(air.SoleBeneficiaryId, service.SoleBeneficiaryId);
            Assert.Equal(air.Id, Assert.Single(service.CoveredServices).CoveredOrderServiceId);
            Assert.Equal(outcome.OrderItemId, service.OrderItemId);
            Assert.Equal(ServicePriceTreatment.SeparatelyPriced, service.PriceTreatment);
        }

        [Fact]
        public async Task A_multi_service_bundle_round_trips_with_one_item_price()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var totalBefore = order.CustomerTotal;

            harness.Quotes.Quote(ProductAdditionFactory.RoundTripBaggageBundle(order, 500_000m));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(2, outcome.ServiceIds.Count);
            Assert.Equal(totalBefore + 500_000m, reloaded.CustomerTotal);
            Assert.Single(reloaded.PricingLines.Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId));
            Assert.All(outcome.ServiceIds, id => Assert.Equal(
                ServicePriceTreatment.Included,
                reloaded.OrderServices.Single(service => service.Id == id).PriceTreatment));
        }

        [Fact]
        public async Task The_item_service_links_round_trip_with_the_change()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.RoundTripBaggageBundle(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            var reloaded = await ReloadAsync(order.Id);

            var links = reloaded.ItemServiceLinks
                .Where(link => link.LinkedByChangeId == outcome.OrderChangeId)
                .ToList();

            Assert.Equal(2, links.Count);
            Assert.All(links, link => Assert.Equal(outcome.OrderItemId, link.OrderItemId));
            Assert.Equal(outcome.ServiceIds.Order(), links.Select(link => link.OrderServiceId).Order());
        }

        [Fact]
        public async Task The_price_change_set_and_operation_evidence_round_trip()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            var reloaded = await ReloadAsync(order.Id);
            var change = reloaded.Changes.Single(candidate => candidate.Id == outcome.OrderChangeId);
            var set = reloaded.PriceChangeSets.Single(candidate => candidate.Id == outcome.PriceChangeSetId);

            Assert.Equal(OrderChangeType.AddProduct, change.ChangeType);
            Assert.Equal(outcome.OperationId, change.OperationId);
            Assert.Equal(change.Id, set.ChangeId);
            Assert.Equal(PriceChangeReason.AddProduct, set.Reason);
            Assert.Equal(2, set.FinancialSequence);
            Assert.True(set.IsCommitted);
        }

        [Fact]
        public async Task The_current_total_equals_the_accepted_pricing_line_truth()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var totalBefore = order.CustomerTotal;

            harness.Quotes.Quote(ProductAdditionFactory.SeatWithTaxAndCommission(order, 200_000m, 20_000m, 10_000m));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            var reloaded = await ReloadAsync(order.Id);

            var accepted = reloaded.PricingLines
                .Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId && line.AffectsCustomerBalance)
                .Sum(line => line.SignedSaleAmount);

            Assert.Equal(220_000m, accepted);
            Assert.Equal(totalBefore + 220_000m, reloaded.CustomerTotal);
            Assert.Equal(reloaded.CustomerTotal, reloaded.Amount.GrandTotal);
            Assert.Equal(10_000m, reloaded.Commission.CommissionAmount);
        }

        [Fact]
        public async Task A_generic_service_round_trips_with_its_schema_and_attributes()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.ExtraSeat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            var reloaded = await ReloadAsync(order.Id);
            var service = reloaded.OrderServices.Single(candidate => candidate.Id == outcome.ServiceIds.Single());

            Assert.Equal("ExtraSeat", service.GenericDetail!.SchemaName);
            Assert.Equal("""{"capacityQuantity":1,"reason":"CBBG"}""", service.GenericDetail!.AttributesJson);
            Assert.Equal(ServiceDocumentKind.ElectronicMiscDocument, service.DocumentKind);
            Assert.True(service.RequiresReservation);
        }

        [Fact]
        public async Task A_hotel_and_a_ground_transport_change_round_trip()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Hotel(order));
            harness.Quotes.Quote(ProductAdditionFactory.GroundTransport(order) with { QuotedOfferId = GroundOfferId });

            await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);
            await harness.OrderChange.AddServiceAsync(order.Id, Selection(GroundOfferId), NewKey(), 2);

            var reloaded = await ReloadAsync(order.Id);

            var hotel = reloaded.OrderServices.Single(service => service.ServiceType == OrderServiceType.HotelStay);
            var ground = reloaded.OrderServices.Single(service => service.ServiceType == OrderServiceType.GroundTransport);

            Assert.Equal(2, hotel.Beneficiaries.Count);
            Assert.Equal(new DateOnly(2026, 10, 4), hotel.HotelDetail!.CheckOut);
            Assert.Equal(2, ground.Beneficiaries.Count);
            Assert.Equal("VAN", ground.GroundTransportDetail!.VehicleTypeCode);
            Assert.Equal(3, reloaded.CommercialVersion);
            Assert.Equal(3, reloaded.FinancialSequence);
        }

        [Fact]
        public async Task An_expired_quote_changes_nothing()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));
            harness.Quotes.Expire(ProductAdditionFactory.QuotedOfferId, ProductAdditionFactory.SelectedOfferItemId);

            var exception = await Assert.ThrowsAsync<Framework.Core.Domain.Exceptions.BusinessException>(
                () => harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1));

            Assert.Equal(20165, exception.Code);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(1, reloaded.CommercialVersion);
            Assert.Equal(1, reloaded.FinancialSequence);
            Assert.Equal(order.CustomerTotal, reloaded.CustomerTotal);
            Assert.DoesNotContain(reloaded.Changes, change => change.ChangeType == OrderChangeType.AddProduct);
        }

        [Fact]
        public async Task The_unique_order_operation_constraint_exists()
        {
            await using var context = _fixture.NewCommandContext();

            var index = await context.Database
                .SqlQuery<string>($"""
                    SELECT i.name AS [Value]
                    FROM sys.indexes i
                    JOIN sys.tables t ON t.object_id = i.object_id
                    JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = 'Order' AND t.name = 'OrderChanges'
                      AND i.is_unique = 1 AND i.has_filter = 1
                    """)
                .ToListAsync();

            Assert.Contains("IX_OrderChanges_OrderId_OperationId", index);
        }

        [Fact]
        public async Task The_local_order_view_contains_the_changed_order()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            await using var query = _fixture.NewQueryContext();

            var details = await query.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == order.Id);

            Assert.Contains(outcome.OrderItemId.ToString(), details.SnapshotJson);
            Assert.Contains(outcome.ServiceIds.Single().ToString(), details.SnapshotJson);
            Assert.Contains(outcome.OrderChangeId.ToString(), details.SnapshotJson);
            Assert.Contains(outcome.PriceChangeSetId.ToString(), details.SnapshotJson);
            Assert.Contains("\"ProductSnapshot\"", details.SnapshotJson);
            Assert.Contains("\"CommercialTerms\"", details.SnapshotJson);
            Assert.Contains("\"PricingHistory\"", details.SnapshotJson);
            Assert.Contains(ProductAdditionFactory.QuotedOfferId, details.SnapshotJson);
        }

        [Fact]
        public async Task The_search_projection_reflects_the_new_total_and_version()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var totalBefore = order.Amount.GrandTotal;

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order, 200_000m));

            await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            await using var query = _fixture.NewQueryContext();

            var row = await query.Orders.AsNoTracking().SingleAsync(candidate => candidate.Id == order.Id);

            Assert.Equal(totalBefore + 200_000m, row.GrandTotal);
            Assert.Equal(2, row.CommercialVersion);
        }

        [Fact]
        public async Task A_ticketed_air_order_with_a_pending_emd_ancillary_reloads_correctly()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var ticketed = await ReloadAsync(order.Id);

            Assert.Equal(OrderStatus.Ticketed, ticketed.Status);

            harness.Quotes.Quote(ProductAdditionFactory.EmdBaggage(ticketed));

            var outcome = await harness.OrderChange.AddServiceAsync(
                order.Id, Selection(), NewKey(), ticketed.CommercialVersion);

            var reloaded = await ReloadAsync(order.Id);
            var baggage = reloaded.OrderServices.Single(service => service.Id == outcome.ServiceIds.Single());

            Assert.Equal(OrderStatus.Ticketed, reloaded.Status);
            Assert.True(reloaded.IsElectronicTicketingComplete());
            Assert.Equal(OrderServiceDocumentStatus.Pending, baggage.DocumentStatus);
            Assert.Equal(ServiceDocumentKind.ElectronicMiscDocument, baggage.DocumentKind);
            Assert.All(reloaded.AirTransportServices, service =>
                Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus));
        }

        [Fact]
        public async Task Adding_an_ancillary_after_ticketing_creates_no_document_state()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            await using (var before = _fixture.NewCommandContext())
            {
                var tickets = await before.ElectronicTickets
                    .Include(ticket => ticket.Coupons)
                    .Where(ticket => ticket.CurrentServicingOrderId == order.Id)
                    .ToListAsync();

                Assert.Equal(2, tickets.Count);
                Assert.All(tickets, ticket => Assert.Equal(2, ticket.Coupons.Count));
            }

            var ticketed = await ReloadAsync(order.Id);

            harness.Quotes.Quote(ProductAdditionFactory.EmdBaggage(ticketed));

            await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), ticketed.CommercialVersion);

            await using var after = _fixture.NewCommandContext();

            var reloadedTickets = await after.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .Where(ticket => ticket.CurrentServicingOrderId == order.Id)
                .ToListAsync();

            Assert.Equal(2, reloadedTickets.Count);
            Assert.All(reloadedTickets, ticket => Assert.Equal(2, ticket.Coupons.Count));
            Assert.All(reloadedTickets.SelectMany(ticket => ticket.Coupons), coupon =>
                Assert.NotEqual(TicketCouponFinancialStatus.Void, coupon.FinancialStatus));
        }

        [Fact]
        public async Task The_added_service_still_reserves_nothing_by_itself()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.ExtraSeat(order));

            var outcome = await harness.OrderChange.AddServiceAsync(order.Id, Selection(), NewKey(), 1);

            await using var context = _fixture.NewCommandContext();

            var reservations = await context.FulfillmentReservations
                .Include(reservation => reservation.Services)
                .Where(reservation => reservation.OrderId == order.Id)
                .ToListAsync();

            Assert.DoesNotContain(
                reservations.SelectMany(reservation => reservation.Services),
                service => service.OrderServiceId == outcome.ServiceIds.Single());
        }

        [Fact]
        public async Task More_than_one_selected_offer_item_fails_without_partial_execution()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var exception = await Assert.ThrowsAsync<Framework.Core.Domain.Exceptions.BusinessException>(
                () => harness.OrderChange.AddServiceAsync(
                    order.Id,
                    [new SelectedQuotedOffer(ProductAdditionFactory.QuotedOfferId, ["A", "B"])],
                    NewKey(),
                    1));

            Assert.Equal(20167, exception.Code);
            Assert.Equal(0, harness.Quotes.CallCount);

            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(1, reloaded.CommercialVersion);
            Assert.DoesNotContain(reloaded.Changes, change => change.ChangeType == OrderChangeType.AddProduct);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static IReadOnlyList<SelectedQuotedOffer> Selection(
            string quotedOfferId = ProductAdditionFactory.QuotedOfferId,
            string selectedOfferItemId = ProductAdditionFactory.SelectedOfferItemId)
            => [new SelectedQuotedOffer(quotedOfferId, [selectedOfferItemId])];

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            var order = await new OrderRepository(context).GetAsync(orderId);

            Assert.NotNull(order);

            return order!;
        }
    }
}
