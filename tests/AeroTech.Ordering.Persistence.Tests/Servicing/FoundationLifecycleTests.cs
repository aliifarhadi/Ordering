using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.ProviderInteractionAggregate;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.ReferenceData.ReadModels;
using AeroTech.Ordering.ServiceHost.OperatorContext;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.Servicing
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class FoundationLifecycleTests
    {
        private const long HomeAirlineId = 7401;

        private static readonly DateTimeOffset Lease = new(2026, 9, 8, 11, 0, 0, TimeSpan.Zero);

        private readonly OrderingDatabaseFixture _fixture;

        public FoundationLifecycleTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task One_command_resolves_to_one_receipt_one_operation_and_one_blocking_claim_across_recovery()
        {
            await SeedHomeOperatorAsync();

            var caller = TestCallerContexts.AgencyUser(11, "subject-a");
            var idempotencyKey = Guid.NewGuid().ToString("N");
            var orderId = NewId();

            var first = await RunAsync(caller, idempotencyKey, orderId, ServicingOperationKind.Cancel);

            Assert.False(first.Receipt.IsReplay);
            Assert.Equal(first.Receipt.OperationId, first.Operation.OperationId);
            Assert.Equal(1, first.Claim.Generation);
            Assert.Equal(HomeAirlineId, first.Receipt.OwnerAirlineId);
            Assert.Equal(HomeAirlineId, first.Operation.OwnerAirlineId);

            var recovered = await RunAsync(caller, idempotencyKey, orderId, ServicingOperationKind.Cancel);

            Assert.True(recovered.Receipt.IsReplay);
            Assert.Equal(first.Receipt.ReceiptId, recovered.Receipt.ReceiptId);
            Assert.Equal(first.Receipt.OperationId, recovered.Receipt.OperationId);
            Assert.Equal(first.Operation.OperationId, recovered.Operation.OperationId);
            Assert.Equal(2, recovered.Claim.Generation);

            await using var verification = _fixture.NewCommandContext();

            Assert.Equal(1, await verification.Set<CommandReceipt>().AsNoTracking()
                .CountAsync(receipt => receipt.IdempotencyKey == idempotencyKey));
            Assert.Equal(1, await verification.Set<ServicingOperation>().AsNoTracking()
                .CountAsync(operation => operation.Id == first.Operation.OperationId));
            Assert.Equal(1, await verification.Set<OperationOrderClaim>().AsNoTracking()
                .CountAsync(claim => claim.OrderId == orderId && claim.IsBlocking));
        }

        [Fact]
        public async Task The_superseded_generation_is_fenced_and_the_current_one_finalizes()
        {
            await SeedHomeOperatorAsync();

            var caller = TestCallerContexts.AgencyUser(12, "subject-b");
            var idempotencyKey = Guid.NewGuid().ToString("N");
            var orderId = NewId();

            var first = await RunAsync(caller, idempotencyKey, orderId, ServicingOperationKind.Issue);
            var recovered = await RunAsync(caller, idempotencyKey, orderId, ServicingOperationKind.Issue);

            await using var context = _fixture.NewCommandContext();
            var claims = NewClaimStore(context);

            var stale = await Assert.ThrowsAsync<BusinessException>(
                () => claims.EnsureCurrentGenerationAsync(orderId, first.Operation.OperationId, first.Claim.Generation));
            Assert.Equal(2702, stale.Code);

            await claims.EnsureCurrentGenerationAsync(orderId, recovered.Operation.OperationId, recovered.Claim.Generation);
            await claims.ResolveAsync(orderId, recovered.Operation.OperationId, recovered.Claim.Generation);
            await context.SaveChangesAsync();

            await using var verification = _fixture.NewCommandContext();

            Assert.False(await verification.Set<OperationOrderClaim>().AsNoTracking()
                .AnyAsync(claim => claim.OrderId == orderId && claim.IsBlocking));
        }

        [Fact]
        public async Task A_duplicate_request_body_conflict_creates_no_second_operation()
        {
            await SeedHomeOperatorAsync();

            var caller = TestCallerContexts.AgencyUser(13, "subject-c");
            var idempotencyKey = Guid.NewGuid().ToString("N");
            var orderId = NewId();

            var first = await RunAsync(caller, idempotencyKey, orderId, ServicingOperationKind.Cancel);

            await using var context = _fixture.NewCommandContext();
            await using var reference = _fixture.NewReferenceContext();

            var conflict = await Assert.ThrowsAsync<BusinessException>(
                () => NewReceiptStore(context, reference, caller).AcquireAsync(ServicingOperationKind.Cancel.ToString(), idempotencyKey, "a-different-body"));

            Assert.Equal(2703, conflict.Code);

            await using var verification = _fixture.NewCommandContext();

            Assert.Equal(1, await verification.Set<ServicingOperation>().AsNoTracking()
                .CountAsync(operation => operation.Id == first.Operation.OperationId));
            Assert.Equal(1, await verification.Set<CommandReceipt>().AsNoTracking()
                .CountAsync(receipt => receipt.IdempotencyKey == idempotencyKey));
        }

        [Fact]
        public async Task Pending_domain_state_cannot_be_committed_by_a_receipt_write()
        {
            await SeedHomeOperatorAsync();

            var caller = TestCallerContexts.AgencyUser(14, "subject-d");

            await using var context = _fixture.NewCommandContext();
            await using var reference = _fixture.NewReferenceContext();

            var pending = PendingDomainState();
            context.Add(pending);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewReceiptStore(context, reference, caller)
                    .AcquireAsync(ServicingOperationKind.Cancel.ToString(), Guid.NewGuid().ToString("N"), "hash"));

            Assert.Equal(2707, error.Code);
            Assert.Contains(nameof(ProviderInteraction), error.Message);

            await using var verification = _fixture.NewCommandContext();

            Assert.False(await verification.Set<ProviderInteraction>().AsNoTracking()
                .AnyAsync(interaction => interaction.Id == pending.Id));
        }

        [Fact]
        public async Task Pending_domain_state_cannot_be_committed_by_a_claim_write()
        {
            await using var context = _fixture.NewCommandContext();

            context.Add(PendingDomainState());

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => NewClaimStore(context).AcquireAsync(NewId(), NewId(), Lease));

            Assert.Equal(2707, error.Code);
        }

        [Fact]
        public async Task An_inbox_marker_may_still_commit_together_with_a_foundation_write()
        {
            await SeedHomeOperatorAsync();

            var caller = TestCallerContexts.AgencyUser(15, "subject-e");
            var messageId = Guid.NewGuid();

            await using var context = _fixture.NewCommandContext();
            await using var reference = _fixture.NewReferenceContext();

            new Persistence.Inbox.InboxStore(context, new OrderingDatabaseFixture.FixedClock())
                .EnlistProcessed(messageId, "/foundation", "TestMessage");

            var receipt = await NewReceiptStore(context, reference, caller)
                .AcquireAsync(ServicingOperationKind.Cancel.ToString(), Guid.NewGuid().ToString("N"), "hash");

            await using var verification = _fixture.NewCommandContext();

            Assert.True(await verification.Set<Persistence.Inbox.InboxMessage>().AsNoTracking()
                .AnyAsync(message => message.MessageId == messageId));
            Assert.True(await verification.Set<CommandReceipt>().AsNoTracking()
                .AnyAsync(row => row.Id == receipt.ReceiptId));
        }

        private static ProviderInteraction PendingDomainState()
            => ProviderInteraction.Create(
                NewId(),
                NewId(),
                null,
                OrderProviderType.Airline,
                null,
                ProviderInteractionType.CreateHold,
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                "{}");

        private async Task<Lifecycle> RunAsync(
            ICallerContext caller,
            string idempotencyKey,
            long orderId,
            ServicingOperationKind kind)
        {
            await using var context = _fixture.NewCommandContext();
            await using var reference = _fixture.NewReferenceContext();

            var receipt = await NewReceiptStore(context, reference, caller)
                .AcquireAsync(kind.ToString(), idempotencyKey, "request-hash");

            var claim = await NewClaimStore(context).AcquireAsync(orderId, receipt.OperationId, Lease);

            var operation = await new ServicingOperationStore(
                    context,
                    new ReferenceDataHomeOperatorProvider(reference),
                    new OrderingDatabaseFixture.FixedClock())
                .PrepareAsync(receipt.OperationId, orderId, kind, "request-hash", claim.Generation, receipt.ReceiptId);

            return new Lifecycle(receipt, operation, claim);
        }

        private sealed record Lifecycle(
            CommandReceiptResult Receipt,
            ServicingOperationRecord Operation,
            OperationClaim Claim);

        private static CommandReceiptStore NewReceiptStore(
            OrderingDbContext context,
            ReferenceData.Persistence.ReferenceDbContext reference,
            ICallerContext caller)
            => new(
                context,
                new ReferenceDataHomeOperatorProvider(reference),
                caller,
                new TestIdGenerator(),
                new OrderingDatabaseFixture.FixedClock());

        private static OperationClaimStore NewClaimStore(OrderingDbContext context)
            => new(context, new TestIdGenerator(), new OrderingDatabaseFixture.FixedClock());

        private async Task SeedHomeOperatorAsync()
        {
            await using var reference = _fixture.NewReferenceContext();

            var existing = await reference.OperatorSettings
                .SingleOrDefaultAsync(row => row.ScopeKey == OperatorScopeKey.HomeOperator);

            if (existing is null)
            {
                reference.OperatorSettings.Add(new OperatorSettingsReadModel
                {
                    Id = 1,
                    ScopeKey = OperatorScopeKey.HomeOperator,
                    HomeAirlineId = HomeAirlineId,
                    LastUpdateTime = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.HomeAirlineId = HomeAirlineId;
            }

            await reference.SaveChangesAsync();
        }

        private static long NewId() => DateTime.UtcNow.Ticks + Random.Shared.Next(1, 1_000_000);

        private sealed class TestIdGenerator : IIdGenerator
        {
            private long _next = DateTime.UtcNow.Ticks;

            public long NewId() => Interlocked.Increment(ref _next);
        }
    }
}
