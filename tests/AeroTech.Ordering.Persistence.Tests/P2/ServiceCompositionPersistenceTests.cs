using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServiceCompositionPersistenceTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public ServiceCompositionPersistenceTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task An_air_service_and_its_detail_round_trip()
        {
            await using var harness = NewHarness();

            var created = await harness.CreateOrderAsync();
            var reloaded = await ReloadAsync(created.Id);

            Assert.Equal(4, reloaded.AirTransportServices.Count());

            foreach (var service in reloaded.AirTransportServices)
            {
                var expected = created.OrderServices.Single(candidate => candidate.Id == service.Id);

                Assert.NotNull(service.AirTransportDetail);
                Assert.Equal(expected.AirTransportDetail!.OrderSegmentId, service.AirTransportDetail!.OrderSegmentId);
                Assert.Equal(expected.AirTransportDetail!.TransitionalFareBasis, service.AirTransportDetail!.TransitionalFareBasis);
                Assert.Equal(1, service.AttachedDetailCount);
            }
        }

        [Fact]
        public async Task Transitional_baggage_evidence_on_the_air_detail_round_trips()
        {
            await using var harness = NewHarness();

            var source = AncillaryFactory.MapAirDetails(
                MultiPassengerOrderFactory.AcceptedSource(harness.Clock),
                air => air with
                {
                    TransitionalCheckedBaggage = new AcceptedBaggageAllowance(2, 23m, BaggageWeightUnit.Kg),
                    TransitionalCabinBaggage = new AcceptedBaggageAllowance(1, 7m, BaggageWeightUnit.Kg)
                });

            var created = await harness.CreateOrderAsync(CreateFrom(harness, source));
            var reloaded = await ReloadAsync(created.Id);

            foreach (var service in reloaded.AirTransportServices)
            {
                Assert.Equal(2, service.AirTransportDetail!.TransitionalCheckedBaggage!.Pieces);
                Assert.Equal(23m, service.AirTransportDetail!.TransitionalCheckedBaggage!.Weight);
                Assert.Equal(BaggageWeightUnit.Kg, service.AirTransportDetail!.TransitionalCheckedBaggage!.Unit);
                Assert.Equal(1, service.AirTransportDetail!.TransitionalCabinBaggage!.Pieces);
            }
        }

        [Fact]
        public async Task A_seat_service_and_its_detail_round_trip()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.Seat(seatNumber: "12A"));

            var seat = Of(reloaded, OrderServiceType.SeatAssignment);

            Assert.Equal("12A", seat.SeatDetail!.SoldSeatNumber);
            Assert.Contains(reloaded.AirTransportServices, air => air.Id == seat.SeatDetail!.AssociatedAirOrderServiceId);
        }

        [Fact]
        public async Task A_baggage_service_and_its_detail_round_trip()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.Baggage(
                kind: BaggageServiceKind.ExcessWeight,
                pieces: 1,
                weight: 12m,
                unit: BaggageWeightUnit.Kg,
                perPieceWeightLimit: 23m));

            var detail = Of(reloaded, OrderServiceType.BaggageAllowance).BaggageDetail!;

            Assert.Equal(BaggageServiceKind.ExcessWeight, detail.Kind);
            Assert.Equal(1, detail.Pieces);
            Assert.Equal(12m, detail.Weight);
            Assert.Equal(BaggageWeightUnit.Kg, detail.WeightUnit);
            Assert.Equal(23m, detail.PerPieceWeightLimit);
        }

        [Fact]
        public async Task A_meal_service_and_its_detail_round_trip()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.Meal(quantity: 2));

            var detail = Of(reloaded, OrderServiceType.Meal).MealDetail!;

            Assert.Equal("VGML", detail.MealCode);
            Assert.Equal(2, detail.Quantity);
            Assert.Equal("VG", detail.SpecialMealCode);
        }

        [Fact]
        public async Task A_lounge_service_and_its_detail_round_trip()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.Lounge(
                airportId: 200,
                guestCount: 2,
                relatedAirServiceRef: AncillaryFactory.OutboundAirServiceRef()));

            var detail = Of(reloaded, OrderServiceType.LoungeAccess).LoungeDetail!;

            Assert.Equal(200, detail.AirportId);
            Assert.Equal(2, detail.GuestCount);
            Assert.Equal("LNG-A", detail.LoungeCode);
            Assert.Contains(reloaded.AirTransportServices, air => air.Id == detail.RelatedAirOrderServiceId);
        }

        [Fact]
        public async Task A_hotel_service_and_its_detail_round_trip()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.Hotel(roomCount: 2, guestCount: 2, nights: 3));

            var detail = Of(reloaded, OrderServiceType.HotelStay).HotelDetail!;

            Assert.Equal("PROP-1", detail.PropertyReference);
            Assert.Equal(new DateOnly(2026, 10, 1), detail.CheckIn);
            Assert.Equal(new DateOnly(2026, 10, 4), detail.CheckOut);
            Assert.Equal(2, detail.RoomCount);
            Assert.Equal("RATE-1", detail.RatePlanReference);
        }

        [Fact]
        public async Task A_ground_transport_service_and_its_detail_round_trip()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.GroundTransport(passengerCount: 3));

            var detail = Of(reloaded, OrderServiceType.GroundTransport).GroundTransportDetail!;

            Assert.Equal("LOC-A", detail.PickupLocationReference);
            Assert.Equal("LOC-B", detail.DropoffLocationReference);
            Assert.Equal(3, detail.PassengerCount);
            Assert.Equal("VAN", detail.VehicleTypeCode);
        }

        [Fact]
        public async Task A_generic_service_keeps_its_schema_and_attributes()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.ExtraSeat());

            var extraSeat = Of(reloaded, OrderServiceType.ExtraSeat);

            Assert.Equal("ExtraSeat", extraSeat.GenericDetail!.SchemaName);
            Assert.Equal("1.0", extraSeat.GenericDetail!.SchemaVersion);
            Assert.Equal("""{"capacityQuantity":1,"reason":"CBBG"}""", extraSeat.GenericDetail!.AttributesJson);
            Assert.Equal(ServiceDocumentKind.ElectronicMiscDocument, extraSeat.DocumentKind);
        }

        [Fact]
        public async Task Beneficiaries_round_trip_for_shared_and_single_beneficiary_services()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.Hotel(), AncillaryFactory.Meal());

            Assert.Equal(2, Of(reloaded, OrderServiceType.HotelStay).Beneficiaries.Count);
            Assert.Single(Of(reloaded, OrderServiceType.Meal).Beneficiaries);
            Assert.All(reloaded.AirTransportServices, service => Assert.Single(service.Beneficiaries));
        }

        [Fact]
        public async Task Service_and_segment_coverage_round_trip()
        {
            var bySegment = AncillaryFactory.Baggage("BAG-SEG") with
            {
                CoveredAirServiceRefs = null,
                CoveredSegmentRefs = [AncillaryFactory.OutboundSegmentRef()]
            };

            var reloaded = await CreateAndReloadAsync(AncillaryFactory.Meal(), bySegment);

            var meal = Of(reloaded, OrderServiceType.Meal);
            var baggage = Of(reloaded, OrderServiceType.BaggageAllowance);

            Assert.Single(meal.CoveredServices);
            Assert.Empty(meal.CoveredSegments);
            Assert.Single(baggage.CoveredSegments);
            Assert.Empty(baggage.CoveredServices);
        }

        [Fact]
        public async Task Item_service_links_round_trip()
        {
            var reloaded = await CreateAndReloadAsync(AncillaryFactory.Meal());

            Assert.Equal(reloaded.OrderServices.Count, reloaded.ItemServiceLinks.Count);
            Assert.All(reloaded.ItemServiceLinks, link =>
            {
                Assert.Contains(reloaded.Items, item => item.Id == link.OrderItemId);
                Assert.Contains(reloaded.OrderServices, service => service.Id == link.OrderServiceId);
                Assert.NotEqual(0L, link.LinkedByChangeId);
            });
        }

        [Fact]
        public async Task Every_persisted_service_has_exactly_one_typed_detail_row()
        {
            var reloaded = await CreateAndReloadAsync(
                AncillaryFactory.Seat(),
                AncillaryFactory.Baggage(),
                AncillaryFactory.Meal(),
                AncillaryFactory.Lounge(),
                AncillaryFactory.Hotel(),
                AncillaryFactory.GroundTransport(),
                AncillaryFactory.Priority());

            Assert.Equal(11, reloaded.OrderServices.Count);
            Assert.All(reloaded.OrderServices, service => Assert.Equal(1, service.AttachedDetailCount));
        }

        [Fact]
        public async Task The_legacy_air_transport_service_table_no_longer_exists()
        {
            await using var context = _fixture.NewCommandContext();

            var exists = await context.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS [Value] FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE s.name = 'Order' AND t.name = 'OrderAirTransportServices'")
                .SingleAsync();

            Assert.Equal(0, exists);
        }

        [Fact]
        public async Task A_reloaded_order_still_reserves_and_issues()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            var reserved = await harness.Reserve.ReserveAsync(order.Id, Guid.NewGuid().ToString("N"), null);
            Assert.Equal(FulfillmentReservationStatus.Confirmed, reserved.ReservationStatus);

            var issued = await harness.Issue.IssueAsync(order.Id, Guid.NewGuid().ToString("N"), null);
            Assert.Equal(ProviderOperationOutcome.Confirmed, issued.Outcome);
            Assert.Equal(2, issued.Tickets.Count);

            var reloaded = await ReloadAsync(order.Id);

            Assert.All(reloaded.AirTransportServices, service =>
            {
                Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus);
                Assert.NotNull(service.ElectronicTicketId);
                Assert.NotNull(service.TicketCouponId);
                Assert.NotNull(service.AirTransportDetail);
                Assert.Single(service.Beneficiaries);
            });
        }

        [Fact]
        public async Task An_ancillary_order_reserves_and_issues_only_what_needs_it()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var source = AncillaryFactory.SourceWith(harness.Clock, AncillaryFactory.Meal(), AncillaryFactory.Baggage());
            var order = await harness.CreateOrderAsync(CreateFrom(harness, source));

            await harness.Reserve.ReserveAsync(order.Id, Guid.NewGuid().ToString("N"), null);
            await harness.Issue.IssueAsync(order.Id, Guid.NewGuid().ToString("N"), null);

            var reloaded = await ReloadAsync(order.Id);

            Assert.All(reloaded.AirTransportServices, service =>
                Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus));

            foreach (var serviceType in new[] { OrderServiceType.Meal, OrderServiceType.BaggageAllowance })
            {
                var ancillary = Of(reloaded, serviceType);

                Assert.False(ancillary.RequiresDocument);
                Assert.Null(ancillary.TrafficDocumentId);
                Assert.Null(ancillary.ElectronicTicketId);
                Assert.Equal(OrderServiceDocumentStatus.Pending, ancillary.DocumentStatus);
            }
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static Order CreateFrom(OrderSliceHarness harness, AcceptedOrderSource source)
            => Order.Create(
                MultiPassengerOrderFactory.Args(),
                source,
                MultiPassengerOrderFactory.OwnerAirlineId,
                harness.Ids,
                harness.Clock);

        private async Task<Order> CreateAndReloadAsync(params AcceptedService[] services)
        {
            await using var harness = NewHarness();

            var source = AncillaryFactory.SourceWith(harness.Clock, services);
            var created = await harness.CreateOrderAsync(CreateFrom(harness, source));

            return await ReloadAsync(created.Id);
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            var order = await new OrderRepository(context).GetAsync(orderId);

            Assert.NotNull(order);

            return order!;
        }

        private static OrderService Of(Order order, OrderServiceType serviceType)
            => order.OrderServices.Single(service => service.ServiceType == serviceType);
    }
}
