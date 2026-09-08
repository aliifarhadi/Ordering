using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Domain.Ports.Funding;
using AeroTech.Ordering.Domain.Ports.Reservation;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.DocumentStockAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Providers.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P1
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OrderCreationAndStockTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public OrderCreationAndStockTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task Creating_an_order_persists_the_full_structure_and_projects_it()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var created = await harness.Create.CreateAsync(
                MultiPassengerOrderFactory.Args(),
                MultiPassengerOrderFactory.AcceptedSource(harness.Clock),
                NewKey());

            Assert.False(created.IsReplay);
            Assert.Equal(1, created.CommercialVersion);
            Assert.Equal(CommercialSummary.Active, created.CommercialSummary);

            await using var verification = _fixture.NewCommandContext();

            var order = await verification.Orders
                .Include(candidate => candidate.Travellers)
                .Include(candidate => candidate.Segments)
                .Include(candidate => candidate.Items)
                .Include(candidate => candidate.OrderServices)
                .SingleAsync(candidate => candidate.Id == created.OrderId);

            Assert.Equal(2, order.Travellers.Count);
            Assert.Equal(2, order.Segments.Count);
            Assert.Equal(4, order.OrderServices.Count);

            await using var query = _fixture.NewQueryContext();
            Assert.True(await query.OrderDetails.AnyAsync(details => details.Id == created.OrderId));
            Assert.True(await query.OrderTravellers.CountAsync(traveller => traveller.OrderId == created.OrderId) == 2);
        }

        [Fact]
        public async Task A_retried_create_with_the_same_key_returns_the_original_order()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var key = NewKey();
            var args = MultiPassengerOrderFactory.Args();

            var first = await harness.Create.CreateAsync(args, MultiPassengerOrderFactory.AcceptedSource(harness.Clock), key);
            var replay = await harness.Create.CreateAsync(args, MultiPassengerOrderFactory.AcceptedSource(harness.Clock), key);

            Assert.False(first.IsReplay);
            Assert.True(replay.IsReplay);
            Assert.Equal(first.OrderId, replay.OrderId);
            Assert.Equal(first.ReceiptId, replay.ReceiptId);

            await using var verification = _fixture.NewCommandContext();

            Assert.Equal(1, await verification.Orders.CountAsync(order => order.Id == first.OrderId));
            Assert.Equal(1, await verification.Set<Persistence.Operations.CommandReceipt>()
                .CountAsync(receipt => receipt.IdempotencyKey == key));
        }

        [Fact]
        public async Task The_same_create_key_with_a_different_body_is_a_conflict()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var key = NewKey();

            await harness.Create.CreateAsync(
                MultiPassengerOrderFactory.Args(),
                MultiPassengerOrderFactory.AcceptedSource(harness.Clock),
                key);

            var different = MultiPassengerOrderFactory.Args() with { CustomerId = 999 };

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Create.CreateAsync(different, MultiPassengerOrderFactory.AcceptedSource(harness.Clock), key));

            Assert.Equal(2703, error.Code);
        }

        [Fact]
        public async Task Concurrent_allocation_from_one_stock_never_issues_a_duplicate_number()
        {
            var stockId = NewId();
            var ids = SequentialIdGenerator.Unique();

            await using (var seed = _fixture.NewCommandContext())
            {
                seed.Set<DocumentStock>().Add(DocumentStock.Define(
                    stockId,
                    OrderSliceHarness.HomeAirlineId,
                    null,
                    "ETKT-CONCURRENCY",
                    Random.Shared.Next(100, 999).ToString(),
                    10,
                    DocumentStock.NoCheckDigitProfile,
                    1,
                    1000));

                await seed.SaveChangesAsync();
            }

            var attempts = Enumerable.Range(1, 8).Select(index => Task.Run(async () =>
            {
                await using var context = _fixture.NewCommandContext();
                var stocks = new DocumentStockRepository(context);
                var stock = await stocks.GetAsync(stockId);

                try
                {
                    var allocation = stock!.Allocate(NewId(), $"Ticket:{index}", ids, new OrderingDatabaseFixture.FixedClock());
                    await context.SaveChangesAsync();
                    return allocation.DocumentNumber;
                }
                catch (DbUpdateException)
                {
                    return null;
                }
            }));

            var numbers = (await Task.WhenAll(attempts)).Where(number => number is not null).ToList();

            Assert.NotEmpty(numbers);
            Assert.Equal(numbers.Count, numbers.Distinct().Count());

            await using var verification = _fixture.NewCommandContext();

            var persisted = await verification.Set<DocumentStockAllocation>()
                .AsNoTracking()
                .Where(allocation => allocation.DocumentStockId == stockId)
                .Select(allocation => allocation.DocumentNumber)
                .ToListAsync();

            Assert.Equal(persisted.Count, persisted.Distinct().Count());
        }

        [Fact]
        public void The_deterministic_adapters_are_test_scaffolding_and_are_not_production_evidence()
        {
            Assert.Equal("AeroTech.Ordering.Providers.Testing", typeof(DeterministicReservationAdapter).Namespace);
            Assert.Equal("AeroTech.Ordering.Providers.Testing", typeof(DeterministicFundingCoverageAdapter).Namespace);
            Assert.Equal("AeroTech.Ordering.Providers.Testing", typeof(DeterministicDocumentIssuanceAdapter).Namespace);

            Assert.Equal("Providers:UseDeterministicTestAdapters", DeterministicAdapterOptions.EnabledKey);
        }

        [Fact]
        public void No_production_adapter_implements_a_p1_port_yet_so_none_can_be_mistaken_for_one()
        {
            var portAssembly = typeof(IReservationPort).Assembly;
            var productionAssembly = typeof(DeterministicReservationAdapter).Assembly;

            var implementations = productionAssembly.GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false })
                .Where(type =>
                    typeof(IReservationPort).IsAssignableFrom(type)
                    || typeof(IFundingCoveragePort).IsAssignableFrom(type)
                    || typeof(IDocumentIssuancePort).IsAssignableFrom(type))
                .ToList();

            Assert.NotNull(portAssembly);
            Assert.All(implementations, type =>
                Assert.Equal("AeroTech.Ordering.Providers.Testing", type.Namespace));
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static long NewId() => DateTime.UtcNow.Ticks + Random.Shared.Next(1, 1_000_000);
    }
}
