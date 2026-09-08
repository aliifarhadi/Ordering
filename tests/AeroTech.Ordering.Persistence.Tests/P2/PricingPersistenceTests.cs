using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P2
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class PricingPersistenceTests
    {
        private const int Currency = MultiPassengerOrderFactory.CurrencyId;

        private readonly OrderingDatabaseFixture _fixture;

        public PricingPersistenceTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_reloaded_order_carries_its_commercial_change_and_price_change_history()
        {
            await using var harness = NewHarness();
            var created = await harness.CreateOrderAsync();

            await using (var reload = _fixture.NewCommandContext())
            {
                var order = await new Persistence.OrderAggregate.OrderRepository(reload).GetAsync(created.Id);

                Assert.NotNull(order);
                Assert.Contains(order!.Changes, change => change.ChangeType == OrderChangeType.Create);
                Assert.Contains(order.PriceChangeSets, set => set.Reason == PriceChangeReason.OriginalSale);
                Assert.Equal(1, order.FinancialSequence);
                Assert.All(order.PriceChangeSets, set => Assert.True(set.IsCommitted));
                Assert.Equal(created.CustomerTotal, order.CustomerTotal);
            }

            var totalBeforeFee = created.CustomerTotal;

            await AppendFeeAsync(harness, created.Id, 25_000m, "LATE-FEE", "1");

            await using (var reload = _fixture.NewCommandContext())
            {
                var order = await new Persistence.OrderAggregate.OrderRepository(reload).GetAsync(created.Id);

                Assert.NotNull(order);
                Assert.Equal(2, order!.FinancialSequence);
                Assert.Equal([1, 2], order.PriceChangeSets.Select(set => set.FinancialSequence).Order());
                Assert.Contains(order.Changes, change => change.ChangeType == OrderChangeType.AddProduct);
                Assert.Contains(order.PricingLines, line => line.Code == "LATE-FEE");
                Assert.Equal(totalBeforeFee + 25_000m, order.CustomerTotal);
            }
        }

        [Fact]
        public async Task The_same_source_occurrence_may_repeat_across_price_change_sets_but_not_within_one()
        {
            await using var harness = NewHarness();
            var created = await harness.CreateOrderAsync();

            await AppendFeeAsync(harness, created.Id, 10_000m, "REPRICE", "1");
            await AppendFeeAsync(harness, created.Id, 10_000m, "REPRICE", "1");

            await using var verification = _fixture.NewCommandContext();

            var lines = await verification.Set<OrderPricingLine>()
                .AsNoTracking()
                .Where(line => line.OrderId == created.Id && line.SourceLineRef == "REPRICE")
                .ToListAsync();

            Assert.Equal(2, lines.Count);
            Assert.Equal(2, lines.Select(line => line.PriceChangeSetId).Distinct().Count());
        }

        [Fact]
        public async Task The_unique_index_is_scoped_to_the_price_change_set()
        {
            await using var harness = NewHarness();
            var created = await harness.CreateOrderAsync();

            await using var verification = _fixture.NewCommandContext();

            var line = await verification.Set<OrderPricingLine>()
                .AsNoTracking()
                .FirstAsync(candidate => candidate.OrderId == created.Id && candidate.SourceLineRef != null);

            var exception = await Assert.ThrowsAsync<SqlException>(() => verification.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO [Order].[OrderPricingLines]
                    ([Id],[OrderId],[PriceChangeSetId],[ComponentType],[Effect],[Direction],[LineRole],
                     [OriginalAmount],[OriginalCurrencyId],[SaleAmount],[SaleCurrencyId],[BasisType],
                     [Refundability],[CreatedAt],[SourceLineRef],[OccurrenceKey],[LastUpdateTime])
                VALUES
                    ({0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},SYSDATETIMEOFFSET(),{13},{14},SYSDATETIMEOFFSET());
                """,
                line.Id + 1_000_000,
                line.OrderId,
                line.PriceChangeSetId,
                (int)line.ComponentType,
                (int)line.Effect,
                (int)line.Direction,
                (int)line.LineRole,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                (int)line.BasisType,
                (int)line.Refundability,
                line.SourceLineRef!,
                line.OccurrenceKey!));

            Assert.Contains("IX_OrderPricingLines_PriceChangeSetId_SourceLineRef_OccurrenceKey", exception.Message);
        }

        [Fact]
        public async Task An_allocation_residual_survives_the_round_trip()
        {
            await using var harness = NewHarness();
            var created = await harness.CreateOrderAsync();
            var serviceId = created.OrderServices.First().Id;

            var order = await harness.Orders.GetAsync(created.Id);

            order!.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.AddProduct,
                    PriceChangeReason.AddProduct,
                    PricingSource.PricingEngine,
                    [
                        Fee(100_000m, "BUNDLE", "1") with
                        {
                            AllocationSets =
                            [
                                new AcceptedPricingAllocationSetArgs(
                                    PricingAllocationPurpose.CommercialValue,
                                    PricingSource.OfferProvider,
                                    PricingAllocationMethod.SourceProvided,
                                    PricingAllocationCompleteness.Partial,
                                    [new AcceptedPricingAllocationArgs(70_000m, Currency, OrderServiceId: serviceId)])
                            ]
                        }
                    ]),
                harness.Ids,
                harness.Clock);

            await harness.UnitOfWork.SaveChangesAsync();

            await using var verification = _fixture.NewCommandContext();

            var set = await verification.Set<OrderPricingAllocationSet>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.OrderIdAtCreation == created.Id);

            Assert.Equal(PricingAllocationCompleteness.Partial, set.Completeness);
            Assert.Equal(30_000m, set.ResidualSaleAmount);
            Assert.Equal(Currency, set.ResidualSaleCurrencyId);
            Assert.Null(set.ResidualOriginalAmount);
        }

        private async Task AppendFeeAsync(
            OrderSliceHarness harness,
            long orderId,
            decimal amount,
            string sourceLineRef,
            string occurrenceKey)
        {
            var order = await harness.Orders.GetAsync(orderId);

            order!.CommitPriceChange(
                new AcceptedPriceChangeArgs(
                    OrderChangeType.AddProduct,
                    PriceChangeReason.AddProduct,
                    PricingSource.PricingEngine,
                    [Fee(amount, sourceLineRef, occurrenceKey)]),
                harness.Ids,
                harness.Clock);

            await harness.UnitOfWork.SaveChangesAsync();
        }

        private static AcceptedPricingLineArgs Fee(decimal amount, string sourceLineRef, string occurrenceKey)
            => new(
                PricingComponentType.Fee,
                PricingEffect.CustomerBalance,
                OrderPricingLineDirection.Debit,
                PricingLineRole.Original,
                amount,
                Currency,
                amount,
                Currency,
                PricingBasisType.Order,
                RefundabilityRule.NonRefundable,
                Code: sourceLineRef,
                SourceLineRef: sourceLineRef,
                OccurrenceKey: occurrenceKey);

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));
    }
}
