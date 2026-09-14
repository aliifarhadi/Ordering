using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal static class ServicingCrashWindow
    {
        public static async Task<ServicingOperation> OperationAsync(
            OrderingDatabaseFixture fixture,
            long orderId,
            ServicingOperationKind kind)
        {
            await using var context = fixture.NewCommandContext();

            return await context.Set<ServicingOperation>()
                .AsNoTracking()
                .SingleAsync(operation => operation.OrderId == orderId && operation.Kind == kind);
        }

        public static async Task<ServicingExternalEvidence> EvidenceAsync(
            OrderingDatabaseFixture fixture,
            long operationId,
            ServicingEvidenceStage stage)
        {
            await using var context = fixture.NewCommandContext();

            var evidence = await new ServicingExternalEvidenceStore(context, new OrderingDatabaseFixture.FixedClock())
                .ListAsync(operationId);

            return evidence.Single(candidate => candidate.Stage == stage);
        }

        public static async Task ExpireRecoveryLeaseAsync(OrderingDatabaseFixture fixture, long orderId)
        {
            await using var context = fixture.NewCommandContext();

            await context.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[OperationOrderClaims] SET [RecoveryLeaseUntil] = {0} WHERE [OrderId] = {1} AND [IsBlocking] = 1",
                new OrderingDatabaseFixture.FixedClock().GetDateTime().AddSeconds(-1),
                orderId);
        }
    }
}
