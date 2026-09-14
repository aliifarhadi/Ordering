using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Servicing
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingOperationExecutionBoundaryTests
    {
        private const long ClaimGeneration = 1;

        private readonly OrderingDatabaseFixture _fixture;

        public ServicingOperationExecutionBoundaryTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_prepared_operation_durably_crosses_the_dispatch_boundary()
        {
            var operationId = await OperationInAsync(ServicingOperationStatus.Prepared);

            await using (var executing = NewHarness())
                await executing.OperationStore.BeginExecutionAsync(operationId, ClaimGeneration + 1);

            var stored = await StoredAsync(operationId);

            Assert.Equal(ServicingOperationStatus.Executing, stored.Status);
            Assert.Equal(ClaimGeneration + 1, stored.ClaimGeneration);
        }

        [Theory]
        [InlineData(ServicingOperationStatus.Completed)]
        [InlineData(ServicingOperationStatus.Rejected)]
        [InlineData(ServicingOperationStatus.Executing)]
        [InlineData(ServicingOperationStatus.AwaitingExternal)]
        [InlineData(ServicingOperationStatus.NeedsReconciliation)]
        public async Task An_operation_that_has_left_prepared_never_crosses_the_dispatch_boundary_again(
            ServicingOperationStatus status)
        {
            var operationId = await OperationInAsync(status);

            await using (var executing = NewHarness())
            {
                var refusal = await Assert.ThrowsAsync<BusinessException>(
                    () => executing.OperationStore.BeginExecutionAsync(operationId, ClaimGeneration + 1));

                Assert.Equal(20334, refusal.Code);

                await executing.CommandContext.SaveChangesAsync();
            }

            var stored = await StoredAsync(operationId);

            Assert.Equal(status, stored.Status);
            Assert.Equal(ClaimGeneration, stored.ClaimGeneration);
        }

        private async Task<long> OperationInAsync(ServicingOperationStatus status)
        {
            var operationId = NewId();

            await using var setup = NewHarness();

            await setup.SeedPlatformAsync();
            await setup.OperationStore.PrepareAsync(
                operationId, NewId(), ServicingOperationKind.Cancel, "request-hash", ClaimGeneration);

            if (status != ServicingOperationStatus.Prepared)
            {
                await setup.OperationStore.TransitionAsync(operationId, status, ClaimGeneration);
                await setup.CommandContext.SaveChangesAsync();
            }

            return operationId;
        }

        private async Task<ServicingOperation> StoredAsync(long operationId)
        {
            await using var context = _fixture.NewCommandContext();

            return await context.Set<ServicingOperation>()
                .AsNoTracking()
                .SingleAsync(operation => operation.Id == operationId);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7438, $"execution-boundary-{Guid.NewGuid():N}"));

        private static long NewId() => DateTime.UtcNow.Ticks + Random.Shared.Next(1, 1_000_000);
    }
}
