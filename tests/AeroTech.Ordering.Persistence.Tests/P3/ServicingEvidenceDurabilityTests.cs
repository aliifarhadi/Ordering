using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.Servicing.Reconciliation;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingEvidenceDurabilityTests
    {
        private const ServicingEvidenceStage Stage = ServicingEvidenceStage.DocumentVoid;
        private const string Reference = "VOID-DURABLE";

        private readonly OrderingDatabaseFixture _fixture;
        private readonly long _operationId;

        public ServicingEvidenceDurabilityTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;
            _operationId = Random.Shared.NextInt64(800_000_000_000, 899_999_999_999);
        }

        [Fact]
        public async Task D1_Terminal_evidence_survives_a_rolled_back_caller_transaction()
        {
            await using (var caller = _fixture.NewCommandContext())
            {
                await using var transaction = await caller.Database.BeginTransactionAsync();

                var store = new ServicingExternalEvidenceStore(
                    caller, new TestClock());

                await store.RecordAsync(
                    _operationId, Stage, ProviderOperationOutcome.Confirmed, Reference, "issuer confirmed");

                await transaction.RollbackAsync();
            }

            await using var reader = _fixture.NewCommandContext();

            var row = await reader.Set<ServicingExternalEvidenceRow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate =>
                    candidate.OperationId == _operationId && candidate.Stage == Stage);

            Assert.NotNull(row);
            Assert.Equal(ProviderOperationOutcome.Confirmed, row!.Outcome);
            Assert.Equal(Reference, row.ProviderReference);
        }

        [Fact]
        public async Task D2_Terminal_evidence_survives_a_caller_transaction_that_throws()
        {
            await using (var caller = _fixture.NewCommandContext())
            {
                await using var transaction = await caller.Database.BeginTransactionAsync();

                var store = new ServicingExternalEvidenceStore(
                    caller, new TestClock());

                await store.RecordAsync(
                    _operationId, Stage, ProviderOperationOutcome.Confirmed, Reference, null);

                try
                {
                    await caller.Database.ExecuteSqlRawAsync("SELECT 1/0");
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                }
            }

            await using var reader = _fixture.NewCommandContext();
            var reading = new ServicingExternalEvidenceStore(
                reader, new TestClock());

            var evidence = Assert.Single(await reading.ListAsync(_operationId));

            Assert.Equal(ProviderOperationOutcome.Confirmed, evidence.Outcome);
            Assert.True(evidence.IsConfirmed);
        }

        [Fact]
        public async Task D3_Durable_confirmed_evidence_is_adopted_with_zero_provider_calls()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup);
            var key = NewKey();
            var caller = TestCallerContexts.AirlineUser(7438, $"durable-{Guid.NewGuid():N}");

            long operationId;
            int versionBeforeAdoption;

            await using (var suspended = new OrderSliceHarness(_fixture, caller))
            {
                suspended.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Unknown;

                var outcome = await suspended.VoidDocument.VoidAsync(
                    issued.OrderId, issued.TicketId, VoidReason.AgentError, "durability", 7, key);

                operationId = outcome.OperationId;

                Assert.Equal(ServicingOperationStatus.AwaitingExternal, outcome.OperationStatus);
                Assert.Single(suspended.DocumentVoids.ObservedVoidKeys);
            }

            var beforeAdoption = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(ElectronicTicketStatus.Issued, beforeAdoption.StatusSummary);

            versionBeforeAdoption = beforeAdoption.DocumentVersion;

            await using (var checkpointing = new OrderSliceHarness(_fixture, caller))
            {
                await checkpointing.ServicingEvidence.RecordAsync(
                    operationId,
                    ServicingEvidenceStage.DocumentVoid,
                    ProviderOperationOutcome.Confirmed,
                    Reference,
                    null,
                    AccountableDocumentKind.ElectronicTicket,
                    beforeAdoption.DocumentNumber);
            }

            await using var resuming = new OrderSliceHarness(_fixture, caller);

            var durable = Assert.Single(await resuming.ServicingEvidence.ListAsync(operationId));

            Assert.Equal(ProviderOperationOutcome.Confirmed, durable.Outcome);

            var resumed = await resuming.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, VoidReason.AgentError, "durability", 7, key);

            Assert.Equal(operationId, resumed.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            Assert.Empty(resuming.DocumentVoids.ObservedVoidKeys);
            Assert.Empty(resuming.DocumentVoids.ObservedRecoveryKeys);

            var adopted = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(ElectronicTicketStatus.Voided, adopted.StatusSummary);
            Assert.All(
                adopted.Coupons,
                coupon => Assert.Equal(TicketCouponFinancialStatus.Void, coupon.FinancialStatus));
            Assert.Equal(versionBeforeAdoption + 1, adopted.DocumentVersion);
            Assert.Equal(Reference, durable.ProviderReference);

            await using var replaying = new OrderSliceHarness(_fixture, caller);

            await replaying.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, VoidReason.AgentError, "durability", 7, key);

            var afterReplay = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(adopted.DocumentVersion, afterReplay.DocumentVersion);
            Assert.Equal(adopted.PriceLinks.Count, afterReplay.PriceLinks.Count);
            Assert.Empty(replaying.DocumentVoids.ObservedVoidKeys);
            Assert.Empty(replaying.DocumentVoids.ObservedRecoveryKeys);
            Assert.Single(await replaying.ServicingEvidence.ListAsync(operationId));
        }

        [Fact]
        public async Task D4_A_void_confirmed_before_a_failed_local_commit_is_adopted_once_by_the_same_operation()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup);
            var caller = TestCallerContexts.AirlineUser(7438, $"durable-{Guid.NewGuid():N}");
            var voids = new DeterministicDocumentVoidAdapter();
            var key = NewKey();
            var before = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            await using (var crashing = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                crashing.Events.FailCommit = true;

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => crashing.VoidDocument.VoidAsync(
                        issued.OrderId, issued.TicketId, VoidReason.AgentError, "crash window", 7, key));
            }

            var crashed = await ServicingCrashWindow.OperationAsync(
                _fixture, issued.OrderId, ServicingOperationKind.VoidDocument);
            var durable = await ServicingCrashWindow.EvidenceAsync(_fixture, crashed.Id, Stage);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Equal(ProviderOperationOutcome.Confirmed, durable.Outcome);
            Assert.Equal($"VOID-{before.DocumentNumber}", durable.ProviderReference);
            Assert.Equal(
                ElectronicTicketStatus.Issued,
                (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).StatusSummary);
            Assert.Single(voids.ObservedVoidKeys);

            await ServicingCrashWindow.ExpireRecoveryLeaseAsync(_fixture, issued.OrderId);

            await using (var resuming = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var resumed = await resuming.VoidDocument.VoidAsync(
                    issued.OrderId, issued.TicketId, VoidReason.AgentError, "crash window", 7, key);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            }

            Assert.Single(voids.ObservedVoidKeys);
            Assert.Empty(voids.ObservedRecoveryKeys);

            var adopted = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(ElectronicTicketStatus.Voided, adopted.StatusSummary);
            Assert.Equal(before.DocumentVersion + 1, adopted.DocumentVersion);

            await using (var replaying = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var replay = await replaying.VoidDocument.VoidAsync(
                    issued.OrderId, issued.TicketId, VoidReason.AgentError, "crash window", 7, key);

                Assert.Equal(crashed.Id, replay.OperationId);
                Assert.True(replay.IsReplay);
            }

            var afterReplay = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(adopted.DocumentVersion, afterReplay.DocumentVersion);
            Assert.Equal(adopted.PriceLinks.Count, afterReplay.PriceLinks.Count);
            Assert.Single(voids.ObservedVoidKeys);
            Assert.Empty(voids.ObservedRecoveryKeys);
            Assert.Equal(
                ProviderOperationOutcome.Confirmed,
                (await ServicingCrashWindow.EvidenceAsync(_fixture, crashed.Id, Stage)).Outcome);
        }

        [Fact]
        public async Task D5_A_void_confirmation_contradicted_by_a_competing_durable_one_enters_reconciliation()
        {
            await using var setup = NewHarness();
            var issued = await IssuedAsync(_fixture, setup);
            CompetingConfirmationEvidenceStore? competing = null;

            await using var voiding = new OrderSliceHarness(
                _fixture,
                TestCallerContexts.AirlineUser(7438, $"durable-{Guid.NewGuid():N}"),
                decorateEvidence: store => competing = new CompetingConfirmationEvidenceStore(store));

            var outcome = await voiding.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, VoidReason.AgentError, "contradiction", 7, NewKey());

            var durable = await ServicingCrashWindow.EvidenceAsync(_fixture, outcome.OperationId, Stage);
            var operation = await ServicingCrashWindow.OperationAsync(
                _fixture, issued.OrderId, ServicingOperationKind.VoidDocument);

            Assert.True(competing!.Competed);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, operation.Status);
            Assert.Equal(ProviderOperationOutcome.Confirmed, durable.Outcome);
            Assert.Equal(CompetingConfirmationEvidenceStore.CompetingReference, durable.ProviderReference);
            Assert.Equal(CompetingConfirmationEvidenceStore.CompetingDetail, durable.Detail);
            Assert.Equal(
                ElectronicTicketStatus.Issued,
                (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).StatusSummary);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7438, $"durable-{Guid.NewGuid():N}"));
    }
}
