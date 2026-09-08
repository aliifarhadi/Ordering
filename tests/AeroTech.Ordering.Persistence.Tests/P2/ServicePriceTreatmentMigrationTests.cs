using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Migrations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Offer.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicePriceTreatmentMigrationTests
    {
        private const decimal Fare = 1_000_000m;
        private const decimal Tax = 90_000m;

        private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

        private readonly OrderingDatabaseFixture _fixture;
        private readonly AirPriceOfferNormalizer _normalizer = new();

        public ServicePriceTreatmentMigrationTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_direct_valued_service_is_corrected_to_separately_priced()
        {
            var orderId = await PersistAsync(_normalizer.Normalize(AirPriceOfferFixture.Offer(Now)));

            await ForceLegacyDefaultAsync(orderId, ServicePriceTreatment.SupplierOpaque);
            await RunBackfillAsync();

            Assert.All(await TreatmentsAsync(orderId), treatment =>
                Assert.Equal(ServicePriceTreatment.SeparatelyPriced, treatment));
        }

        [Fact]
        public async Task An_item_valued_order_does_not_become_several_separately_priced_services()
        {
            var orderId = await PersistAsync(_normalizer.Normalize(AirPriceOfferFixture.TwoFlightOffer(
                Now,
                [
                    AirPriceOfferFixture.ItemFareLine(Fare),
                    AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)
                ])));

            await ForceLegacyDefaultAsync(orderId, ServicePriceTreatment.SeparatelyPriced);
            await RunBackfillAsync();

            var treatments = await TreatmentsAsync(orderId);

            Assert.Equal(2, treatments.Count);
            Assert.All(treatments, treatment => Assert.Equal(ServicePriceTreatment.Included, treatment));
        }

        [Fact]
        public async Task Historical_evidence_that_proves_no_primary_service_value_becomes_supplier_opaque()
        {
            var orderId = await PersistAsync(_normalizer.Normalize(AirPriceOfferFixture.Offer(
                Now,
                priceLines: [AirPriceOfferFixture.ChargeLine("TAX-1", "I6", Tax)])));

            await ForceLegacyDefaultAsync(orderId, ServicePriceTreatment.SeparatelyPriced);
            await RunBackfillAsync();

            Assert.All(await TreatmentsAsync(orderId), treatment =>
                Assert.Equal(ServicePriceTreatment.SupplierOpaque, treatment));
        }

        [Fact]
        public async Task No_migrated_row_becomes_complimentary_without_explicit_evidence()
        {
            var orderId = await PersistAsync(_normalizer.Normalize(AirPriceOfferFixture.Offer(Now)));

            await ForceLegacyDefaultAsync(orderId, ServicePriceTreatment.SeparatelyPriced);

            var complimentaryBefore = await CountAsync(ServicePriceTreatment.Complimentary);

            await RunBackfillAsync();

            Assert.Equal(complimentaryBefore, await CountAsync(ServicePriceTreatment.Complimentary));
            Assert.DoesNotContain(ServicePriceTreatment.Complimentary, await TreatmentsAsync(orderId));
        }

        [Fact]
        public async Task An_explicitly_complimentary_service_is_never_reclassified()
        {
            var orderId = await PersistAsync(_normalizer.Normalize(AirPriceOfferFixture.Offer(Now)));

            await ForceLegacyDefaultAsync(orderId, ServicePriceTreatment.Complimentary);
            await RunBackfillAsync();

            Assert.All(await TreatmentsAsync(orderId), treatment =>
                Assert.Equal(ServicePriceTreatment.Complimentary, treatment));
        }

        [Fact]
        public async Task The_correction_is_idempotent()
        {
            var orderId = await PersistAsync(_normalizer.Normalize(AirPriceOfferFixture.Offer(Now)));

            await RunBackfillAsync();
            var first = await TreatmentsAsync(orderId);

            await RunBackfillAsync();

            Assert.Equal(first, await TreatmentsAsync(orderId));
        }

        private async Task<long> PersistAsync(AcceptedOrderSource source)
        {
            await using var harness = new OrderSliceHarness(
                _fixture,
                TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

            var order = Order.Create(OrderFactory.Args(), source, OrderFactory.OwnerAirlineId, harness.Ids, harness.Clock);

            return (await harness.CreateOrderAsync(order)).Id;
        }

        private async Task ForceLegacyDefaultAsync(long orderId, ServicePriceTreatment treatment)
        {
            await using var context = _fixture.NewCommandContext();

            await context.Database.ExecuteSqlRawAsync(
                $"UPDATE [Order].[OrderServices] SET [PriceTreatment] = {(int)treatment} WHERE [OrderId] = {orderId}");
        }

        private async Task RunBackfillAsync()
        {
            await using var context = _fixture.NewCommandContext();

            await context.Database.ExecuteSqlRawAsync(P2D1ServicePriceTreatmentBackfill.Sql);
        }

        private async Task<IReadOnlyList<ServicePriceTreatment>> TreatmentsAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            var values = await context.Database
                .SqlQueryRaw<int>($"SELECT [PriceTreatment] AS [Value] FROM [Order].[OrderServices] WHERE [OrderId] = {orderId}")
                .ToListAsync();

            return values.Select(value => (ServicePriceTreatment)value).ToList();
        }

        private async Task<int> CountAsync(ServicePriceTreatment treatment)
        {
            await using var context = _fixture.NewCommandContext();

            return await context.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS [Value] FROM [Order].[OrderServices] WHERE [PriceTreatment] = {(int)treatment}")
                .SingleAsync();
        }
    }
}
