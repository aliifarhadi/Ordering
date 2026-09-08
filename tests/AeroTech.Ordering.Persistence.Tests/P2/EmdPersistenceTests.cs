using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.OrderChange;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class EmdPersistenceTests
    {
        private const int ForeignKeyViolation = 547;
        private const long MissingId = -1L;

        private readonly OrderingDatabaseFixture _fixture;

        public EmdPersistenceTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task The_document_header_round_trips()
        {
            await using var harness = NewHarness();
            var order = await IssuedDocumentOrderAsync(harness);

            var document = Assert.Single(await DocumentsAsync(order.Id));

            Assert.Equal(order.Id, document.OriginalOrderId);
            Assert.Equal(order.Id, document.CurrentServicingOrderId);
            Assert.Equal(ElectronicMiscDocumentType.Associated, document.Type);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceCode, document.ReasonForIssuanceCode);
            Assert.Equal(OrderSliceHarness.HomeAirlineId, document.IssuerCarrierId);
            Assert.Equal(order.AirlineOfficeId, document.IssuingOfficeId);
            Assert.Equal(DocumentAuthority.Local, document.Authority);
            Assert.Equal(ElectronicMiscDocumentStatus.Issued, document.StatusSummary);
            Assert.Equal(order.CurrencyId, document.CurrencyId);
            Assert.Equal(400_000m, document.IssuedTotal);
            Assert.StartsWith("EMD-", document.ProviderReference!, StringComparison.Ordinal);
            Assert.Equal(2, document.DocumentVersion);
        }

        [Fact]
        public async Task The_coupons_and_their_ticket_association_round_trip()
        {
            await using var harness = NewHarness();
            var order = await IssuedDocumentOrderAsync(harness);

            var document = Assert.Single(await DocumentsAsync(order.Id));
            var coupon = Assert.Single(document.Coupons);

            Assert.Equal(1, coupon.CouponNumber);
            Assert.Equal(EmdCouponPurpose.Service, coupon.Purpose);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceSubCode, coupon.ReasonForIssuanceSubCode);
            Assert.Equal(EmdCouponStatus.OpenForUse, coupon.Status);
            Assert.NotNull(coupon.OrderServiceId);
            Assert.NotNull(coupon.AssociatedTicketCouponId);
            Assert.Equal(400_000m, coupon.IssuanceValue);

            await using var context = _fixture.NewCommandContext();

            Assert.True(await context.ElectronicTickets
                .SelectMany(ticket => ticket.Coupons)
                .AnyAsync(candidate => candidate.Id == coupon.AssociatedTicketCouponId));
        }

        [Fact]
        public async Task The_price_links_round_trip()
        {
            await using var harness = NewHarness();
            var order = await IssuedDocumentOrderAsync(harness);

            var document = Assert.Single(await DocumentsAsync(order.Id));
            var link = Assert.Single(document.PriceLinks);
            var reloaded = await ReloadAsync(order.Id);

            Assert.Equal(Assert.Single(document.Coupons).Id, link.EmdCouponId);
            Assert.Equal(400_000m, link.AttributedValue);
            Assert.Equal(order.CurrencyId, link.CurrencyId);
            Assert.Contains(reloaded.PricingLines, line => line.Id == link.PricingLineId);
        }

        [Fact]
        public async Task The_service_issuance_snapshot_round_trips()
        {
            await using var harness = NewHarness();
            var order = await IssuedDocumentOrderAsync(harness);

            var reloaded = await ReloadAsync(order.Id);

            var documented = reloaded.OrderServices.Single(service =>
                service.RequiresDocument && service.DocumentKind == ServiceDocumentKind.ElectronicMiscDocument);

            var snapshot = documented.EmdIssuanceSnapshot;

            Assert.NotNull(snapshot);
            Assert.Equal(ElectronicMiscDocumentType.Associated, snapshot!.EmdType);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceCode, snapshot.ReasonForIssuanceCode);
            Assert.Equal(ProductAdditionFactory.BaggageReasonForIssuanceSubCode, snapshot.ReasonForIssuanceSubCode);
            Assert.NotNull(snapshot.AssociatedAirOrderServiceId);
            Assert.Equal(ProductAdditionFactory.SourceSystem, snapshot.SourceSystem);
            Assert.Equal(ProductAdditionFactory.QuotedOfferId, snapshot.SourceReference);
        }

        [Fact]
        public async Task The_order_view_redisplays_the_document()
        {
            await using var harness = NewHarness();
            var order = await IssuedDocumentOrderAsync(harness);

            var document = Assert.Single(await DocumentsAsync(order.Id));

            await using var query = _fixture.NewQueryContext();

            var details = await query.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == order.Id);

            Assert.Contains("\"MiscellaneousDocuments\"", details.SnapshotJson);
            Assert.Contains(document.DocumentNumber, details.SnapshotJson);
            Assert.Contains(document.ReasonForIssuanceCode, details.SnapshotJson);
            Assert.Contains("\"ReasonForIssuanceSubCode\"", details.SnapshotJson);
            Assert.Contains("\"AssociatedTicketCouponId\"", details.SnapshotJson);
            Assert.Contains(document.ProviderReference!, details.SnapshotJson);
        }

        [Fact]
        public async Task A_coupon_cannot_reference_a_missing_ticket_coupon()
            => await AssertRejectedAsync(
                "UPDATE [Order].[EmdCoupons] SET [AssociatedTicketCouponId] = {0} WHERE [ElectronicMiscDocumentId] IN (SELECT [Id] FROM [Order].[ElectronicMiscDocuments] WHERE [CurrentServicingOrderId] = {1})");

        [Fact]
        public async Task A_coupon_cannot_reference_a_missing_order_service()
            => await AssertRejectedAsync(
                "UPDATE [Order].[EmdCoupons] SET [OrderServiceId] = {0} WHERE [ElectronicMiscDocumentId] IN (SELECT [Id] FROM [Order].[ElectronicMiscDocuments] WHERE [CurrentServicingOrderId] = {1})");

        [Fact]
        public async Task A_price_link_cannot_reference_a_missing_pricing_line()
            => await AssertRejectedAsync(
                "UPDATE [Order].[EmdPriceLinks] SET [PricingLineId] = {0} WHERE [ElectronicMiscDocumentId] IN (SELECT [Id] FROM [Order].[ElectronicMiscDocuments] WHERE [CurrentServicingOrderId] = {1})");

        [Fact]
        public async Task A_price_link_cannot_reference_a_missing_coupon()
            => await AssertRejectedAsync(
                "UPDATE [Order].[EmdPriceLinks] SET [EmdCouponId] = {0} WHERE [ElectronicMiscDocumentId] IN (SELECT [Id] FROM [Order].[ElectronicMiscDocuments] WHERE [CurrentServicingOrderId] = {1})");

        [Fact]
        public async Task An_issuance_snapshot_cannot_reference_a_missing_air_service()
            => await AssertRejectedAsync(
                "UPDATE [Order].[OrderServiceEmdIssuanceSnapshots] SET [AssociatedAirOrderServiceId] = {0} WHERE [OrderServiceId] IN (SELECT [Id] FROM [Order].[OrderServices] WHERE [OrderId] = {1})");

        [Fact]
        public async Task The_document_tables_carry_no_shadow_foreign_keys()
        {
            await using var context = _fixture.NewCommandContext();

            var columns = await context.Database
                .SqlQuery<string>($"""
                    SELECT c.name AS [Value]
                    FROM sys.columns c
                    JOIN sys.tables t ON t.object_id = c.object_id
                    JOIN sys.schemas s ON s.schema_id = t.schema_id
                    WHERE s.name = 'Order'
                      AND t.name IN ('ElectronicMiscDocuments', 'EmdCoupons', 'EmdPriceLinks', 'OrderServiceEmdIssuanceSnapshots')
                    """)
                .ToListAsync();

            Assert.DoesNotContain(columns, column => column.EndsWith("Id1", StringComparison.Ordinal));
            Assert.DoesNotContain(columns, column => column.Contains("TempId", StringComparison.Ordinal));
        }

        [Fact]
        public async Task The_document_tables_exist_with_no_action_delete_rules()
        {
            await using var context = _fixture.NewCommandContext();

            var noAction = await context.Database
                .SqlQuery<string>($"SELECT name AS [Value] FROM sys.foreign_keys WHERE delete_referential_action = 0")
                .ToListAsync();

            foreach (var name in new[]
                     {
                         "FK_EmdCoupons_OrderServices_OrderServiceId",
                         "FK_EmdCoupons_OrderPricingLines_PricingLineId",
                         "FK_EmdCoupons_TicketCoupons_AssociatedTicketCouponId",
                         "FK_EmdPriceLinks_EmdCoupons_EmdCouponId",
                         "FK_EmdPriceLinks_OrderPricingLines_PricingLineId",
                         "FK_EmdPriceLinks_OrderPricingAllocations_AllocationId",
                         "FK_OrderServiceEmdIssuanceSnapshots_OrderServices_AssociatedAirOrderServiceId"
                     })
                Assert.Contains(name, noAction);
        }

        [Fact]
        public async Task The_document_number_is_globally_unique()
        {
            await using var harness = NewHarness();
            var order = await IssuedDocumentOrderAsync(harness);

            var document = Assert.Single(await DocumentsAsync(order.Id));

            await using var context = _fixture.NewCommandContext();

            var duplicate = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlRawAsync(
                $"""
                 INSERT INTO [Order].[ElectronicMiscDocuments]
                     ([Id],[OriginalOrderId],[CurrentServicingOrderId],[TravelerId],[OperationId],[DocumentNumber],[Type],
                      [ReasonForIssuanceCode],[IssuerCarrierId],[IssuingOfficeId],[Authority],[IssuedAt],[IssuedTotal],
                      [CurrencyId],[ProviderReference],[StatusSummary],[DocumentVersion],[LastUpdateTime])
                 SELECT [Id] + 1, [OriginalOrderId], [CurrentServicingOrderId], [TravelerId], [OperationId], [DocumentNumber], [Type],
                        [ReasonForIssuanceCode], [IssuerCarrierId], [IssuingOfficeId], [Authority], [IssuedAt], [IssuedTotal],
                        [CurrencyId], [ProviderReference], [StatusSummary], [DocumentVersion], SYSDATETIMEOFFSET()
                 FROM [Order].[ElectronicMiscDocuments] WHERE [Id] = {document.Id}
                 """));

            Assert.Contains("IX_ElectronicMiscDocuments_DocumentNumber", duplicate.ToString());
        }

        [Fact]
        public async Task The_migration_is_applied_on_the_current_database()
        {
            await using var context = _fixture.NewCommandContext();

            var applied = await context.Database.GetAppliedMigrationsAsync();

            Assert.Contains(applied, migration => migration.EndsWith("P2FElectronicMiscDocument", StringComparison.Ordinal));
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private async Task<Order> IssuedDocumentOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var ticketed = await ReloadAsync(order.Id);
            var accepted = ProductAdditionFactory.EmdBaggage(ticketed);

            harness.Quotes.Quote(accepted);

            await harness.OrderChange.AddServiceAsync(
                order.Id,
                [new SelectedQuotedOffer(ProductAdditionFactory.QuotedOfferId, [accepted.SelectedOfferItemId])],
                NewKey(),
                ticketed.CommercialVersion);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            return await ReloadAsync(order.Id);
        }

        private async Task AssertRejectedAsync(string statementTemplate)
        {
            long orderId;

            await using (var harness = NewHarness())
                orderId = (await IssuedDocumentOrderAsync(harness)).Id;

            await using var context = _fixture.NewCommandContext();

            var statement = string.Format(statementTemplate, MissingId, orderId);

            var exception = await Assert.ThrowsAsync<SqlException>(
                () => context.Database.ExecuteSqlRawAsync(statement));

            Assert.Equal(ForeignKeyViolation, exception.Number);
        }

        private async Task<IReadOnlyList<ElectronicMiscDocument>> DocumentsAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return await new ElectronicMiscDocumentRepository(context).ListByOrderAsync(orderId);
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
