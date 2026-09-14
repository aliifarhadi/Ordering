using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.DocumentVoid;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Providers.Deterministic;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;
using static AeroTech.Ordering.Persistence.Tests.P3.ServicingCrashWindow;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class DocumentVoidDispatchBoundaryTests
    {
        private const ServicingEvidenceStage Stage = ServicingEvidenceStage.DocumentVoid;
        private const VoidReason Reason = VoidReason.AgentError;
        private const long Actor = 7;

        private readonly OrderingDatabaseFixture _fixture;

        public DocumentVoidDispatchBoundaryTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task DA_a_crash_after_the_void_reached_the_provider_recovers_first_and_never_voids_twice()
        {
            var issued = await IssuedTicketAsync();
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { ThrowAfterVoid = true };
            var key = NewKey();
            var before = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            await using (var crashing = new OrderSliceHarness(
                             _fixture, caller, documentVoids: voids,
                             decorateEvidence: store => new UnreachableEvidenceStore(store)))
            {
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(() => VoidAsync(crashing, issued, key));
            }

            var crashed = await OperationAsync(_fixture, issued.OrderId, ServicingOperationKind.VoidDocument);
            var dispatchedKey = Assert.Single(voids.ObservedVoidKeys);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Null(await EvidenceOrNullAsync(_fixture, crashed.Id, Stage));
            Assert.True(await ClaimIsBlockingAsync(_fixture, issued.OrderId));

            await ExpireRecoveryLeaseAsync(_fixture, issued.OrderId);

            voids.ThrowAfterVoid = false;
            voids.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using (var resuming = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var resumed = await VoidAsync(resuming, issued, key);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            }

            Assert.Single(voids.ObservedVoidKeys);
            Assert.Equal(dispatchedKey, Assert.Single(voids.ObservedRecoveryKeys));
            Assert.Equal(ProviderOperationOutcome.Confirmed, (await EvidenceAsync(_fixture, crashed.Id, Stage)).Outcome);

            var voided = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(ElectronicTicketStatus.Voided, voided.StatusSummary);
            Assert.Equal(before.DocumentVersion + 1, voided.DocumentVersion);

            await using (var replaying = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var replay = await VoidAsync(replaying, issued, key);

                Assert.Equal(crashed.Id, replay.OperationId);
                Assert.True(replay.IsReplay);
            }

            Assert.Single(voids.ObservedVoidKeys);
            Assert.Single(voids.ObservedRecoveryKeys);
            Assert.Equal(voided.DocumentVersion, (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).DocumentVersion);
        }

        [Fact]
        public async Task DA2_a_transport_failure_after_the_void_reached_the_provider_keeps_the_order_claimed_until_recovered()
        {
            var issued = await IssuedTicketAsync();
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { ThrowAfterVoid = true };
            var key = NewKey();

            await using (var failing = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => VoidAsync(failing, issued, key));
            }

            var held = await OperationAsync(_fixture, issued.OrderId, ServicingOperationKind.VoidDocument);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, held.Status);
            Assert.Equal(ProviderOperationOutcome.Unknown, (await EvidenceAsync(_fixture, held.Id, Stage)).Outcome);
            Assert.True(await ClaimIsBlockingAsync(_fixture, issued.OrderId));

            await using (var competing = new OrderSliceHarness(_fixture, Caller()))
            {
                var refusal = await Assert.ThrowsAsync<BusinessException>(
                    () => competing.Cancel.CancelAsync(issued.OrderId, Reason, Actor, NewKey(), null));

                Assert.Equal(ClaimConflict, refusal.Code);
            }

            voids.ThrowAfterVoid = false;
            voids.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using (var resuming = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var resumed = await VoidAsync(resuming, issued, key);

                Assert.Equal(held.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            }

            Assert.Single(voids.ObservedVoidKeys);
            Assert.Single(voids.ObservedRecoveryKeys);
            Assert.Equal(
                ElectronicTicketStatus.Voided,
                (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).StatusSummary);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending, ProviderOperationOutcome.Unknown)]
        [InlineData(ProviderOperationOutcome.Pending, ProviderOperationOutcome.Confirmed)]
        [InlineData(ProviderOperationOutcome.Unknown, ProviderOperationOutcome.Unknown)]
        [InlineData(ProviderOperationOutcome.Unknown, ProviderOperationOutcome.Confirmed)]
        public async Task DB_DC_unresolved_void_evidence_survives_a_failed_local_save_and_is_recovered_before_any_second_void(
            ProviderOperationOutcome applied,
            ProviderOperationOutcome recovered)
        {
            var issued = await IssuedTicketAsync();
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { VoidOutcome = applied };
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(() => VoidAsync(crashing, issued, key));
            }

            var crashed = await OperationAsync(_fixture, issued.OrderId, ServicingOperationKind.VoidDocument);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Equal(applied, (await EvidenceAsync(_fixture, crashed.Id, Stage)).Outcome);

            await ExpireRecoveryLeaseAsync(_fixture, issued.OrderId);

            voids.RecoveryOutcome = recovered;

            await using (var resuming = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var resumed = await VoidAsync(resuming, issued, key);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(
                    recovered == ProviderOperationOutcome.Confirmed
                        ? ServicingOperationStatus.Completed
                        : ServicingOperationStatus.NeedsReconciliation,
                    resumed.OperationStatus);
            }

            Assert.Single(voids.ObservedVoidKeys);
            Assert.Single(voids.ObservedRecoveryKeys);
            Assert.Equal(
                recovered == ProviderOperationOutcome.Confirmed
                    ? ProviderOperationOutcome.Confirmed
                    : ProviderOperationOutcome.Unknown,
                (await EvidenceAsync(_fixture, crashed.Id, Stage)).Outcome);
            Assert.Equal(
                recovered == ProviderOperationOutcome.Confirmed
                    ? ElectronicTicketStatus.Voided
                    : ElectronicTicketStatus.Issued,
                (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).StatusSummary);
        }

        [Fact]
        public async Task DD_rejected_void_evidence_survives_a_failed_local_save_and_settles_without_asking_the_provider_again()
        {
            var issued = await IssuedTicketAsync();
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { VoidOutcome = ProviderOperationOutcome.Rejected };
            var key = NewKey();

            await using (var crashing = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                crashing.ServicingUnitOfWork.FailSaves = true;

                await Assert.ThrowsAsync<InvalidOperationException>(() => VoidAsync(crashing, issued, key));
            }

            var crashed = await OperationAsync(_fixture, issued.OrderId, ServicingOperationKind.VoidDocument);
            var rejected = await EvidenceAsync(_fixture, crashed.Id, Stage);

            Assert.Equal(ServicingOperationStatus.Executing, crashed.Status);
            Assert.Equal(ProviderOperationOutcome.Rejected, rejected.Outcome);

            await ExpireRecoveryLeaseAsync(_fixture, issued.OrderId);

            await using (var resuming = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var resumed = await VoidAsync(resuming, issued, key);

                Assert.Equal(crashed.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Rejected, resumed.OperationStatus);
            }

            await using (var replaying = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var replay = await VoidAsync(replaying, issued, key);

                Assert.Equal(ServicingOperationStatus.Rejected, replay.OperationStatus);
                Assert.True(replay.IsReplay);
            }

            var settled = await EvidenceAsync(_fixture, crashed.Id, Stage);

            Assert.Single(voids.ObservedVoidKeys);
            Assert.Empty(voids.ObservedRecoveryKeys);
            Assert.Equal(rejected, settled);
            Assert.False(await ClaimIsBlockingAsync(_fixture, issued.OrderId));
            Assert.Equal(
                ElectronicTicketStatus.Issued,
                (await TicketAsync(_fixture, issued.OrderId, issued.TicketId)).StatusSummary);
        }

        [Fact]
        public async Task DE_a_failed_execution_boundary_never_reaches_the_void_provider()
        {
            var issued = await IssuedTicketAsync();
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter();
            var key = NewKey();
            RefusingExecutionBoundaryOperationStore? boundary = null;

            await using (var refusing = new OrderSliceHarness(
                             _fixture, caller, documentVoids: voids,
                             decorateOperationStore: store => boundary = new RefusingExecutionBoundaryOperationStore(store)))
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => VoidAsync(refusing, issued, key));
            }

            var refused = await OperationAsync(_fixture, issued.OrderId, ServicingOperationKind.VoidDocument);

            Assert.Equal(1, boundary!.Refusals);
            Assert.Empty(voids.ObservedEligibilityKeys);
            Assert.Empty(voids.ObservedVoidKeys);
            Assert.Equal(ServicingOperationStatus.Prepared, refused.Status);
            Assert.Null(await EvidenceOrNullAsync(_fixture, refused.Id, Stage));

            await using (var retrying = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var retried = await VoidAsync(retrying, issued, key);

                Assert.Equal(refused.Id, retried.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, retried.OperationStatus);
            }

            Assert.Single(voids.ObservedVoidKeys);
        }

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending)]
        [InlineData(ProviderOperationOutcome.Unknown)]
        public async Task DF_a_baseline_prepared_void_with_unresolved_evidence_recovers_before_any_second_void(
            ProviderOperationOutcome unresolved)
        {
            var issued = await IssuedTicketAsync();
            var caller = Caller();
            var voids = new DeterministicDocumentVoidAdapter { VoidOutcome = unresolved };
            var key = NewKey();

            await using (var suspending = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var suspended = await VoidAsync(suspending, issued, key);

                Assert.Equal(ServicingOperationStatus.AwaitingExternal, suspended.OperationStatus);
            }

            var legacy = await OperationAsync(_fixture, issued.OrderId, ServicingOperationKind.VoidDocument);

            await DowngradeToPreparedAsync(_fixture, legacy.Id);
            await ExpireRecoveryLeaseAsync(_fixture, issued.OrderId);

            voids.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            await using (var resuming = new OrderSliceHarness(_fixture, caller, documentVoids: voids))
            {
                var resumed = await VoidAsync(resuming, issued, key);

                Assert.Equal(legacy.Id, resumed.OperationId);
                Assert.Equal(ServicingOperationStatus.Completed, resumed.OperationStatus);
            }

            Assert.Single(voids.ObservedVoidKeys);
            Assert.Single(voids.ObservedRecoveryKeys);
        }

        private async Task<IssuedTicket> IssuedTicketAsync()
        {
            await using var setup = new OrderSliceHarness(_fixture, Caller());

            return await IssuedAsync(_fixture, setup);
        }

        private static Task<DocumentVoidOutcome> VoidAsync(OrderSliceHarness harness, IssuedTicket issued, string key)
            => harness.VoidDocument.VoidAsync(issued.OrderId, issued.TicketId, Reason, "dispatch boundary", Actor, key);

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"void-boundary-{Guid.NewGuid():N}");
    }
}
