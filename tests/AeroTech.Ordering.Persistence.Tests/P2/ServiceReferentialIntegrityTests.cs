using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServiceReferentialIntegrityTests
    {
        private const int ForeignKeyViolation = 547;
        private const long MissingId = -1L;

        private readonly OrderingDatabaseFixture _fixture;

        public ServiceReferentialIntegrityTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_beneficiary_cannot_reference_a_missing_traveller()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderServiceBeneficiaries] SET [OrderTravellerId] = {0} WHERE [OrderServiceId] IN (SELECT [Id] FROM [Order].[OrderServices] WHERE [OrderId] = {1})");

        [Fact]
        public async Task A_covered_segment_cannot_reference_a_missing_segment()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderServiceCoveredSegments] SET [OrderSegmentId] = {0} WHERE [OrderServiceId] IN (SELECT [Id] FROM [Order].[OrderServices] WHERE [OrderId] = {1})");

        [Fact]
        public async Task A_covered_service_cannot_reference_a_missing_service()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderServiceCoveredServices] SET [CoveredOrderServiceId] = {0} WHERE [OrderServiceId] IN (SELECT [Id] FROM [Order].[OrderServices] WHERE [OrderId] = {1})");

        [Fact]
        public async Task An_item_service_link_cannot_reference_a_missing_item()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderItemServiceLinks] SET [OrderItemId] = {0} WHERE [OrderId] = {1}");

        [Fact]
        public async Task An_item_service_link_cannot_reference_a_missing_service()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderItemServiceLinks] SET [OrderServiceId] = {0} WHERE [OrderId] = {1}");

        [Fact]
        public async Task An_item_service_link_cannot_reference_a_missing_change()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderItemServiceLinks] SET [LinkedByChangeId] = {0} WHERE [OrderId] = {1}");

        [Fact]
        public async Task An_air_detail_cannot_reference_a_missing_segment()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderAirTransportServiceDetails] SET [OrderSegmentId] = {0} WHERE [OrderServiceId] IN (SELECT [Id] FROM [Order].[OrderServices] WHERE [OrderId] = {1})");

        [Fact]
        public async Task A_seat_detail_cannot_reference_a_missing_associated_service()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderSeatServiceDetails] SET [AssociatedAirOrderServiceId] = {0} WHERE [OrderServiceId] IN (SELECT [Id] FROM [Order].[OrderServices] WHERE [OrderId] = {1})");

        [Fact]
        public async Task A_lounge_related_air_reference_cannot_point_at_a_missing_service()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderLoungeServiceDetails] SET [RelatedAirOrderServiceId] = {0} WHERE [OrderServiceId] IN (SELECT [Id] FROM [Order].[OrderServices] WHERE [OrderId] = {1})");

        [Fact]
        public async Task A_service_cannot_reference_a_missing_current_item()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderServices] SET [OrderItemId] = {0} WHERE [OrderId] = {1}");

        [Fact]
        public async Task A_valid_order_graph_still_persists_and_reloads()
        {
            await using var harness = NewHarness();

            var created = await harness.CreateOrderAsync(NewOrderGraph(harness));

            await using var reload = _fixture.NewCommandContext();

            var reloaded = await new OrderRepository(reload).GetAsync(created.Id);

            Assert.NotNull(reloaded);
            Assert.Equal(created.OrderServices.Count, reloaded!.OrderServices.Count);
            Assert.Equal(created.ItemServiceLinks.Count, reloaded.ItemServiceLinks.Count);
            Assert.Contains(reloaded.OrderServices, service => service.SeatDetail is not null);
            Assert.Contains(reloaded.OrderServices, service => service.LoungeDetail?.RelatedAirOrderServiceId is not null);
            Assert.Contains(reloaded.OrderServices, service => service.CoveredSegments.Count > 0);
            Assert.Contains(reloaded.OrderServices, service => service.CoveredServices.Count > 0);
        }

        [Fact]
        public async Task Every_new_target_foreign_key_exists_and_takes_no_delete_action()
        {
            var expected = new[]
            {
                "FK_OrderServices_OrderItems_OrderItemId",
                "FK_OrderServiceBeneficiaries_OrderTravellers_OrderTravellerId",
                "FK_OrderServiceCoveredSegments_OrderSegments_OrderSegmentId",
                "FK_OrderServiceCoveredServices_OrderServices_CoveredOrderServiceId",
                "FK_OrderItemServiceLinks_OrderItems_OrderItemId",
                "FK_OrderItemServiceLinks_OrderServices_OrderServiceId",
                "FK_OrderItemServiceLinks_OrderChanges_LinkedByChangeId",
                "FK_OrderAirTransportServiceDetails_OrderSegments_OrderSegmentId",
                "FK_OrderSeatServiceDetails_OrderServices_AssociatedAirOrderServiceId",
                "FK_OrderLoungeServiceDetails_OrderServices_RelatedAirOrderServiceId"
            };

            await using var context = _fixture.NewCommandContext();

            var noAction = await context.Database
                .SqlQuery<string>($"SELECT name AS [Value] FROM sys.foreign_keys WHERE delete_referential_action = 0")
                .ToListAsync();

            foreach (var name in expected)
                Assert.Contains(name, noAction);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static Order NewOrderGraph(OrderSliceHarness harness)
        {
            var bySegment = AncillaryFactory.Baggage("BAG-SEG") with
            {
                CoveredAirServiceRefs = null,
                CoveredSegmentRefs = [AncillaryFactory.OutboundSegmentRef()]
            };

            return AncillaryFactory.OrderWith(
                harness.Ids,
                harness.Clock,
                AncillaryFactory.Seat(),
                AncillaryFactory.Baggage(),
                bySegment,
                AncillaryFactory.Lounge(relatedAirServiceRef: AncillaryFactory.OutboundAirServiceRef()));
        }

        private async Task AssertRejectedAsync(string statementTemplate)
        {
            long orderId;

            await using (var harness = NewHarness())
                orderId = (await harness.CreateOrderAsync(NewOrderGraph(harness))).Id;

            await using var context = _fixture.NewCommandContext();

            var statement = string.Format(statementTemplate, MissingId, orderId);

            var exception = await Assert.ThrowsAsync<SqlException>(
                () => context.Database.ExecuteSqlRawAsync(statement));

            Assert.Equal(ForeignKeyViolation, exception.Number);
        }
    }
}
