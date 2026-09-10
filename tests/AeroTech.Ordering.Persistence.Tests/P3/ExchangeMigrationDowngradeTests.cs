using AeroTech.Ordering.Persistence.Migrations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ExchangeMigrationDowngradeTests
    {
        private const string F1Migration = "20260910091715_P3F1Exchange";
        private const string F2Migration = "20260910141733_P3F2MultiCouponExchange";

        private readonly OrderingDatabaseFixture _fixture;

        public ExchangeMigrationDowngradeTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public void The_multi_coupon_downgrade_guard_runs_before_any_destructive_statement()
        {
            using var context = _fixture.NewCommandContext();

            var script = context.GetService<IMigrator>().GenerateScript(F2Migration, F1Migration);

            var guard = script.IndexOf("THROW 51000", StringComparison.Ordinal);
            var drop = script.IndexOf("DROP TABLE", StringComparison.OrdinalIgnoreCase);
            var addColumn = script.IndexOf("ADD [PredecessorTicketCouponId]", StringComparison.OrdinalIgnoreCase);

            Assert.True(guard >= 0, "the down script must carry the multi-coupon guard");
            Assert.True(drop > guard, "the guard must precede dropping the coupon table");
            Assert.True(addColumn > guard, "the guard must precede restoring the single-coupon columns");
            Assert.DoesNotContain("MIN([PredecessorCouponNumber])", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("[Order].[AcceptedExchangePlanCoupons]", script, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_single_coupon_plan_downgrades_but_a_multi_coupon_plan_is_refused()
        {
            await using var context = _fixture.NewCommandContext();
            await using var transaction = await context.Database.BeginTransactionAsync();

            await context.Database.ExecuteSqlRawAsync("DELETE FROM [Order].[AcceptedExchangePlanCoupons]");
            await context.Database.ExecuteSqlRawAsync(PlanSql(9_000_001));
            await context.Database.ExecuteSqlRawAsync(CouponSql(9_000_001, couponId: 11, couponNumber: 1));

            await context.Database.ExecuteSqlRawAsync(P3F2MultiCouponExchange.MultiCouponDowngradeGuard);

            var restored = await RestoreProbeAsync(context);

            Assert.Equal(1, restored);

            await context.Database.ExecuteSqlRawAsync(CouponSql(9_000_001, couponId: 12, couponNumber: 2));

            var refusal = await Assert.ThrowsAsync<SqlException>(
                () => context.Database.ExecuteSqlRawAsync(P3F2MultiCouponExchange.MultiCouponDowngradeGuard));

            Assert.Equal(51_000, refusal.Number);
            Assert.Contains("cannot be reverted", refusal.Message, StringComparison.OrdinalIgnoreCase);

            await transaction.RollbackAsync();
        }

        private static async Task<int> RestoreProbeAsync(OrderingDbContext context)
            => await context.Database.SqlQueryRaw<int>(
                    """
                    SELECT COUNT(*) AS [Value]
                    FROM [Order].[AcceptedExchangePlans] accepted
                    INNER JOIN [Order].[AcceptedExchangePlanCoupons] coupon
                        ON coupon.[OperationId] = accepted.[OperationId]
                    WHERE accepted.[OperationId] = 9000001
                    """)
                .SingleAsync();

        private static string PlanSql(long operationId)
            => $"""
               INSERT INTO [Order].[AcceptedExchangePlans]
                   ([OperationId], [OrderId], [QuotedExchangeId], [SourceSystem], [TargetSelectionRef], [PricingSource],
                    [SaleCurrencyId], [PredecessorElectronicTicketId], [PredecessorDocumentNumber], [PredecessorTravellerId],
                    [SuccessorElectronicTicketId], [ExpectedCommercialVersion], [MonetaryOutcome], [AcceptedPlan],
                    [Disposition], [ReservationOutcome], [CreatedAt], [UpdatedAt])
               VALUES ({operationId}, 1, 'MIG', 'AirPrice', 'MIG-TARGET', 2, 1, 1, 'T000', 1, {operationId + 500}, 1, 1, '[]', 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
               """;

        private static string CouponSql(long operationId, long couponId, int couponNumber)
            => $"""
               INSERT INTO [Order].[AcceptedExchangePlanCoupons]
                   ([OperationId], [PredecessorTicketCouponId], [PredecessorCouponNumber], [PredecessorOrderServiceId],
                    [Disposition], [SuccessorTicketCouponId], [ReplacementOrderServiceId], [ReplacementOrderSegmentId], [SuccessorCouponNumber])
               VALUES ({operationId}, {couponId}, {couponNumber}, {couponId + 100}, 1, {couponId + 200}, {couponId + 300}, {couponId + 400}, {couponNumber});
               """;
    }
}
