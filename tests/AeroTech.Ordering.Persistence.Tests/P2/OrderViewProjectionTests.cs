using AeroTech.Ordering.RestApi.V1.OrderAggregate.Responses;
using System.Reflection;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Query.OrderAggregate.Queries.GetOrderDetails;
using AeroTech.Ordering.Query.OrderAggregate.View;
using AeroTech.Ordering.RestApi.V1.OrderAggregate.Requests;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OrderViewProjectionTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public OrderViewProjectionTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public void The_query_contract_is_strongly_typed()
        {
            var handled = typeof(GetOrderDetailsQuery)
                .GetInterfaces()
                .Single(contract => contract.Name.StartsWith("IRequest`", StringComparison.Ordinal))
                .GetGenericArguments()
                .Single();

            Assert.Equal(typeof(OrderView), Nullable.GetUnderlyingType(handled) ?? handled);
            Assert.NotEqual(typeof(object), handled);
            Assert.DoesNotContain("JsonElement", handled.FullName!, StringComparison.Ordinal);
        }

        [Fact]
        public void The_order_change_response_uses_the_same_view()
        {
            var order = typeof(OrderChangeResponse)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Single(property => property.Name == "Order");

            Assert.Equal(typeof(OrderView), order.PropertyType);
        }

        [Fact]
        public async Task The_view_exposes_root_commercial_and_projection_facts()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var view = await ViewAsync(harness, order.Id);

            Assert.Equal(OrderView.CurrentSchemaVersion, view.SchemaVersion);
            Assert.Equal(order.Id, view.OrderId);
            Assert.Equal(order.UniqueIdentifierId, view.UniqueIdentifierId);
            Assert.Equal(1, view.CommercialVersion);
            Assert.Equal(order.ObligationVersion, view.ObligationVersion);
            Assert.True(view.ProjectionRevision >= 1);
            Assert.Equal(order.CurrencyId, view.CurrencyId);
            Assert.Equal(order.OwnerAirlineId, view.OwnerAirlineId);
            Assert.Equal(order.Amount.GrandTotal, view.Totals.GrandTotal);
            Assert.Equal(order.CustomerTotal, view.Totals.GrandTotal);
        }

        [Fact]
        public async Task The_view_exposes_items_with_their_accepted_snapshots()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var view = await ViewAsync(harness, order.Id);

            Assert.NotEmpty(view.Items);
            Assert.All(view.Items, item =>
            {
                Assert.NotNull(item.ProductSnapshot);
                Assert.NotNull(item.CommercialTerms);
                Assert.Equal(MultiPassengerOrderFactory.SourceSystem, item.ProductSnapshot!.SourceSystem);
                Assert.Equal(MultiPassengerOrderFactory.SourceOfferId, item.ProductSnapshot.SourceOfferId);
                Assert.Null(item.ProductSnapshot.ProductCode);
                Assert.Equal(CommercialTermState.Permitted, item.CommercialTerms!.RefundabilitySummary);
            });
        }

        [Fact]
        public async Task The_view_exposes_air_service_composition()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var view = await ViewAsync(harness, order.Id);
            var air = view.Services.Where(service => service.ServiceType == OrderServiceType.AirTransportation).ToList();

            Assert.Equal(4, air.Count);
            Assert.All(air, service =>
            {
                Assert.Equal("AirTransport", service.Detail.Kind);
                Assert.NotNull(service.Detail.OrderSegmentId);
                Assert.Equal(MultiPassengerOrderFactory.FareBasis, service.Detail.TransitionalFareBasis);
                Assert.Single(service.Beneficiaries);
                Assert.Equal(ServicePriceTreatment.SeparatelyPriced, service.PriceTreatment);
                Assert.True(service.Fulfillment.RequiresDocument);
                Assert.Equal(ServiceDocumentKind.ElectronicTicket, service.Fulfillment.DocumentKind);
                Assert.NotEmpty(service.OriginalOrderItemMembership);
            });
        }

        [Fact]
        public async Task The_view_exposes_ancillary_detail_beneficiaries_and_coverage()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();
            var air = ProductAdditionFactory.OutboundAirService(order);

            await AddAsync(harness, order, ProductAdditionFactory.Seat(order));

            var view = await ViewAsync(harness, order.Id);
            var seat = view.Services.Single(service => service.ServiceType == OrderServiceType.SeatAssignment);

            Assert.Equal("Seat", seat.Detail.Kind);
            Assert.Equal("14C", seat.Detail.SoldSeatNumber);
            Assert.Equal(air.Id, seat.Detail.AssociatedAirOrderServiceId);
            Assert.Equal(air.SoleBeneficiaryId, Assert.Single(seat.Beneficiaries));
            Assert.Equal(air.Id, Assert.Single(seat.Coverage.Services));
            Assert.Empty(seat.Coverage.Segments);
            Assert.False(seat.Fulfillment.RequiresDocument);
        }

        [Fact]
        public async Task A_generic_service_shows_schema_identity_without_raw_attributes()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            await AddAsync(harness, order, ProductAdditionFactory.WiFi(order));

            var view = await ViewAsync(harness, order.Id);
            var wifi = view.Services.Single(service => service.ServiceType == OrderServiceType.WiFi);

            Assert.Equal("Generic", wifi.Detail.Kind);
            Assert.Equal("WiFi", wifi.Detail.SchemaName);
            Assert.Equal("1.0", wifi.Detail.SchemaVersion);

            var json = await SnapshotJsonAsync(order.Id);

            Assert.DoesNotContain("accessKind", json, StringComparison.Ordinal);
            Assert.DoesNotContain("AttributesJson", json, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_view_exposes_no_financial_pseudo_service()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var view = await ViewAsync(harness, order.Id);

            Assert.DoesNotContain(view.Services, service => service.ServiceType is OrderServiceType.ServiceFee
                or OrderServiceType.Penalty
                or OrderServiceType.Credit
                or OrderServiceType.TaxAdjustment);
        }

        [Fact]
        public async Task The_change_operation_returns_the_same_view_shape()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var outcome = await AddAsync(harness, order, ProductAdditionFactory.Seat(order, 200_000m));
            var view = await ViewAsync(harness, order.Id);

            Assert.Equal(2, view.CommercialVersion);
            Assert.Equal(outcome.CustomerTotal, view.Totals.GrandTotal);
            Assert.Contains(view.Items, item => item.OrderItemId == outcome.OrderItemId);
            Assert.Contains(view.Services, service => service.ServiceId == outcome.ServiceIds.Single());
            Assert.Contains(view.PricingHistory, set => set.PriceChangeSetId == outcome.PriceChangeSetId);
            Assert.Contains(view.Changes, change => change.OrderChangeId == outcome.OrderChangeId);
        }

        [Fact]
        public async Task Retrieval_calls_no_upstream_provider()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            harness.Quotes.Quote(ProductAdditionFactory.Seat(order));

            var quotesBefore = harness.Quotes.CallCount;
            var reservationsBefore = harness.Reservation.ObservedOperationKeys.Count;
            var fundingBefore = harness.Funding.ObservedOperationKeys.Count;
            var ticketsBefore = harness.Documents.Requests.Count;
            var documentsBefore = harness.MiscDocuments.Requests.Count;

            await ViewAsync(harness, order.Id);

            Assert.Equal(quotesBefore, harness.Quotes.CallCount);
            Assert.Equal(reservationsBefore, harness.Reservation.ObservedOperationKeys.Count);
            Assert.Equal(fundingBefore, harness.Funding.ObservedOperationKeys.Count);
            Assert.Equal(ticketsBefore, harness.Documents.Requests.Count);
            Assert.Equal(documentsBefore, harness.MiscDocuments.Requests.Count);
        }

        [Fact]
        public async Task The_view_exposes_fare_construction_structure_without_inference()
        {
            await using var harness = NewHarness();

            var source = MultiPassengerOrderFactory.AcceptedSource(harness.Clock) with
            {
                FareConstructions = [FareConstructionFactory.TrueRoundTrip()]
            };

            var order = await harness.CreateOrderAsync(Order.Create(
                MultiPassengerOrderFactory.Args(),
                source,
                MultiPassengerOrderFactory.OwnerAirlineId,
                harness.Ids,
                harness.Clock));

            var view = await ViewAsync(harness, order.Id);
            var construction = Assert.Single(view.FareConstructions);

            Assert.False(string.IsNullOrWhiteSpace(construction.SourceSystem));
            Assert.Null(construction.SupersedesConstructionId);
            Assert.NotEmpty(construction.ItemMembership);

            var group = Assert.Single(construction.PricingGroups);
            var unit = Assert.Single(group.PricingUnits);

            Assert.NotEmpty(group.TravellerIds);
            Assert.Equal(2, unit.FareComponents.Count);
            Assert.All(unit.FareComponents, component => Assert.NotEmpty(component.ServiceIds));
        }

        [Fact]
        public async Task An_order_without_fare_construction_stays_empty_and_valid()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            var view = await ViewAsync(harness, order.Id);

            Assert.Empty(view.FareConstructions);
            Assert.NotEmpty(view.Services);
        }

        [Fact]
        public async Task The_view_keeps_modern_pricing_polarity_and_provenance()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            await AddAsync(
                harness,
                order,
                ProductAdditionFactory.SeatWithTaxAndCommission(order, 200_000m, 20_000m, 10_000m));

            var view = await ViewAsync(harness, order.Id);

            Assert.Equal(2, view.PricingHistory.Count);
            Assert.Equal(
                view.PricingHistory.Select(set => set.FinancialSequence).Order(),
                view.PricingHistory.Select(set => set.FinancialSequence));

            var addition = view.PricingHistory.Single(set => set.Reason == PriceChangeReason.AddProduct);

            Assert.Equal(220_000m, addition.DerivedCustomerBalanceImpact);
            Assert.All(addition.PricingLines, line => Assert.True(line.SaleAmount >= 0m));
            Assert.All(addition.PricingLines, line => Assert.Equal(PricingLineRole.Original, line.LineRole));

            var commission = addition.PricingLines.Single(line => line.ComponentType == PricingComponentType.Commission);
            var charge = addition.PricingLines.Single(line => line.ComponentType == PricingComponentType.ProductCharge);
            var tax = addition.PricingLines.Single(line => line.ComponentType == PricingComponentType.Tax);

            Assert.Equal(PricingEffect.SettlementOnly, commission.Effect);
            Assert.Equal(PricingEffect.CustomerBalance, charge.Effect);
            Assert.Equal(OrderPricingLineDirection.Debit, charge.Direction);
            Assert.Equal(PricingBasisType.OrderService, charge.BasisType);
            Assert.NotNull(tax.SourceLineRef);
            Assert.Equal("1", tax.OccurrenceKey);
            Assert.Equal(order.CurrencyId, charge.SaleCurrencyId);
            Assert.Equal(order.CurrencyId, charge.OriginalCurrencyId);
        }

        [Fact]
        public async Task The_view_redisplays_source_allocations_without_fabrication()
        {
            await using var harness = NewHarness();
            var order = await harness.CreateOrderAsync();

            await AddAsync(harness, order, ProductAdditionFactory.RoundTripBaggageBundle(order, 500_000m));

            var view = await ViewAsync(harness, order.Id);
            var addition = view.PricingHistory.Single(set => set.Reason == PriceChangeReason.AddProduct);
            var line = Assert.Single(addition.PricingLines);

            Assert.Equal(500_000m, line.SaleAmount);
            Assert.Equal(PricingBasisType.OrderItem, line.BasisType);
            Assert.Empty(line.AllocationSets);
        }

        [Fact]
        public async Task The_view_keeps_document_families_independent()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var ticketed = await ReloadAsync(order.Id);
            var commercialVersion = ticketed.CommercialVersion;

            await AddAsync(harness, ticketed, ProductAdditionFactory.EmdBaggage(ticketed));

            var beforeIssue = await ViewAsync(harness, order.Id);

            Assert.True(beforeIssue.Facets.ElectronicTicket.IsComplete);
            Assert.False(beforeIssue.Facets.MiscellaneousDocument.IsComplete);
            Assert.Equal(OrderStatus.Ticketed, beforeIssue.Status);
            Assert.NotEmpty(beforeIssue.ElectronicTickets);
            Assert.Empty(beforeIssue.MiscellaneousDocuments);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var afterIssue = await ViewAsync(harness, order.Id);
            var document = Assert.Single(afterIssue.MiscellaneousDocuments);
            var coupon = Assert.Single(document.Coupons);

            Assert.Equal(commercialVersion + 1, afterIssue.CommercialVersion);
            Assert.True(afterIssue.ProjectionRevision > beforeIssue.ProjectionRevision);
            Assert.Equal(ElectronicMiscDocumentType.Associated, document.Type);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceCode, document.ReasonForIssuanceCode);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceSubCode, coupon.ReasonForIssuanceSubCode);
            Assert.NotNull(coupon.AssociatedTicketCouponId);

            Assert.Contains(
                afterIssue.ElectronicTickets.SelectMany(ticket => ticket.Coupons),
                ticketCoupon => ticketCoupon.CouponId == coupon.AssociatedTicketCouponId);

            Assert.True(afterIssue.Facets.ElectronicTicket.IsComplete);
            Assert.True(afterIssue.Facets.MiscellaneousDocument.IsComplete);
        }

        [Fact]
        public async Task Issuance_advances_the_projection_but_not_the_commercial_version()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var beforeIssue = await ViewAsync(harness, order.Id);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var afterIssue = await ViewAsync(harness, order.Id);

            Assert.Equal(beforeIssue.CommercialVersion, afterIssue.CommercialVersion);
            Assert.True(afterIssue.ProjectionRevision > beforeIssue.ProjectionRevision);
            Assert.NotEmpty(afterIssue.ElectronicTickets);
            Assert.All(afterIssue.Services.Where(service => service.ServiceType == OrderServiceType.AirTransportation),
                service => Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus));
        }

        [Fact]
        public async Task The_view_exposes_reservation_evidence()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var view = await ViewAsync(harness, order.Id);
            var reservation = Assert.Single(view.Reservations);

            Assert.Equal(FulfillmentReservationStatus.Confirmed, reservation.Status);
            Assert.Equal(4, reservation.Members.Count);
            Assert.All(reservation.Members, member =>
                Assert.Equal(ReservationMemberStatus.Confirmed, member.ObservedStatus));
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static async Task<OrderChangeOutcome> AddAsync(
            OrderSliceHarness harness,
            Order order,
            Domain.OrderAggregate.AcceptedSource.ProductAddition.AcceptedAddServiceChange accepted)
        {
            harness.Quotes.Quote(accepted);

            return await harness.OrderChange.AddServiceAsync(
                order.Id,
                [new SelectedQuotedOffer(accepted.QuotedOfferId, [accepted.SelectedOfferItemId])],
                NewKey(),
                order.CommercialVersion);
        }

        private async Task<OrderView> ViewAsync(OrderSliceHarness harness, long orderId)
        {
            await using var query = _fixture.NewQueryContext();

            var view = await new GetOrderDetailsQueryHandler(query)
                .Handle(new GetOrderDetailsQuery(orderId), CancellationToken.None);

            Assert.NotNull(view);

            return view!;
        }

        private async Task<string> SnapshotJsonAsync(long orderId)
        {
            await using var query = _fixture.NewQueryContext();

            var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .SingleAsync(query.OrderDetails, details => details.Id == orderId);

            return row.SnapshotJson;
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            var order = await new OrderRepository(context).GetAsync(orderId);

            Assert.NotNull(order);

            return order!;
        }
    }
}
