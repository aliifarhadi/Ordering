using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reconciliation;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingResolutionAuditTests
    {
        private const VoidReason Reason = VoidReason.AgentError;
        private const string Detail = "operator resolution";
        private const long Actor = 7;

        private readonly OrderingDatabaseFixture _fixture;
        private readonly long _seed;
        private readonly string _manual;

        public ServicingResolutionAuditTests(OrderingDatabaseFixture fixture)
        {
            _fixture = fixture;
            _seed = Random.Shared.NextInt64(10_000_000, 99_999_999);
            _manual = $"R2{_seed}";
        }

        [Fact]
        public async Task R20_A_duplicate_operator_recovery_is_idempotent()
        {
            await using var harness = NewHarness();
            var operationId = await UnresolvedVoidAsync(harness);
            var generation = await GenerationAsync(harness, operationId);

            var first = await harness.Resolutions.RecordAsync(
                Resume(operationId, generation, "operator-1"));

            var second = await harness.Resolutions.RecordAsync(
                Resume(operationId, generation, "operator-1"));

            Assert.False(first.IsReplay);
            Assert.True(second.IsReplay);
            Assert.Equal(first.Resolution.ResolutionId, second.Resolution.ResolutionId);
            Assert.Equal(first.Resolution.RecordedAt, second.Resolution.RecordedAt);

            var audit = await harness.ManualResolutions.ListAsync(operationId);

            Assert.Single(audit);
        }

        [Fact]
        public async Task R21_A_stale_expected_generation_fails_with_no_mutation()
        {
            await using var harness = NewHarness();
            var operationId = await UnresolvedVoidAsync(harness);
            var generation = await GenerationAsync(harness, operationId);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Resolutions.RecordAsync(
                    Resume(operationId, generation - 1, "operator-1")));

            Assert.Equal(20330, error.Code);
            Assert.Equal(409, error.HttpStatus);

            await using var reading = NewHarness();

            Assert.Empty(await reading.ManualResolutions.ListAsync(operationId));
            Assert.Equal(
                ServicingOperationStatus.NeedsReconciliation,
                (await reading.ReconciliationView.FindAsync(operationId))!.Status);
        }

        [Fact]
        public async Task R22_A_second_actor_cannot_duplicate_the_same_resolution()
        {
            await using var harness = NewHarness();
            var operationId = await UnresolvedVoidAsync(harness);
            var generation = await GenerationAsync(harness, operationId);

            await harness.Resolutions.RecordAsync(Resume(operationId, generation, "operator-1"));

            await using var second = NewHarness();
            var replay = await second.Resolutions.RecordAsync(Resume(operationId, generation, "operator-2"));

            Assert.True(replay.IsReplay);
            Assert.Equal("operator-1", replay.Resolution.Actor);

            var audit = Assert.Single(await second.ManualResolutions.ListAsync(operationId));

            Assert.Equal("operator-1", audit.Actor);
        }

        [Fact]
        public async Task R23_The_operator_resolution_audit_survives_reload()
        {
            await using var harness = NewHarness();
            var operationId = await UnresolvedVoidAsync(harness);
            var generation = await GenerationAsync(harness, operationId);

            await harness.Resolutions.RecordAsync(
                new ServicingResolutionExecution(
                    operationId,
                    ServicingResolutionKind.ResumeFromCheckpoint,
                    "operator-1",
                    "issuer confirmed the void out of band",
                    generation,
                    "CASE-4711",
                    ServicingEvidenceStage.DocumentVoid));

            await using var reading = NewHarness();
            var audit = Assert.Single(await reading.ManualResolutions.ListAsync(operationId));

            Assert.Equal(operationId, audit.OperationId);
            Assert.Equal(ServicingResolutionKind.ResumeFromCheckpoint, audit.Kind);
            Assert.Equal("operator-1", audit.Actor);
            Assert.Equal("issuer confirmed the void out of band", audit.Reason);
            Assert.Equal("CASE-4711", audit.Reference);
            Assert.Equal(ServicingEvidenceStage.DocumentVoid, audit.EvidenceStage);
            Assert.Equal(generation, audit.ExpectedClaimGeneration);
            Assert.NotEqual(default, audit.RecordedAt);

            var view = (await reading.ReconciliationView.FindAsync(operationId))!;

            Assert.Equal([audit.ResolutionId], view.ManualResolutions.Select(entry => entry.ResolutionId));
        }

        [Fact]
        public async Task R24_A_resolution_never_settles_the_operation()
        {
            await using var harness = NewHarness();
            var operationId = await UnresolvedVoidAsync(harness);
            var generation = await GenerationAsync(harness, operationId);

            await harness.Resolutions.RecordAsync(Resume(operationId, generation, "operator-1"));

            await using var reading = NewHarness();
            var view = (await reading.ReconciliationView.FindAsync(operationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, view.Status);
            Assert.Equal(generation, view.ClaimGeneration);
            Assert.True(view.IsUnresolved);
            Assert.Equal(ServicingRecoveryAction.ReplayCommand, view.RecoveryAction);
        }

        [Fact]
        public async Task R24b_A_settled_operation_refuses_every_resolution()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            var completed = await harness.VoidDocument.VoidAsync(
                order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            var generation = await GenerationAsync(harness, completed.OperationId);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Resolutions.RecordAsync(
                    Resume(completed.OperationId, generation, "operator-1")));

            Assert.Equal(20329, error.Code);
            Assert.Equal(409, error.HttpStatus);
            Assert.Empty(await harness.ManualResolutions.ListAsync(completed.OperationId));
        }

        [Fact]
        public async Task R24c_A_resume_is_refused_where_only_manual_resolution_is_possible()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ManualReviewExchangeAsync(setup, harness);
            var generation = await GenerationAsync(harness, operationId);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Resolutions.RecordAsync(
                    Resume(operationId, generation, "operator-1")));

            Assert.Equal(20331, error.Code);
            Assert.Empty(await harness.ManualResolutions.ListAsync(operationId));
        }

        [Fact]
        public async Task R24d_A_manual_decision_is_refused_where_a_replay_can_still_resolve_it()
        {
            await using var harness = NewHarness();
            var operationId = await UnresolvedVoidAsync(harness);
            var generation = await GenerationAsync(harness, operationId);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Resolutions.RecordAsync(
                    new ServicingResolutionExecution(
                        operationId,
                        ServicingResolutionKind.RecordManualDecision,
                        "operator-1",
                        "handled by the issuer help desk",
                        generation)));

            Assert.Equal(20331, error.Code);
            Assert.Empty(await harness.ManualResolutions.ListAsync(operationId));
        }

        [Fact]
        public async Task R24e_A_manual_review_operation_accepts_an_escalation_without_settling_it()
        {
            await using var setup = NewHarness();
            await using var harness = NewHarness();

            var operationId = await ManualReviewExchangeAsync(setup, harness);
            var generation = await GenerationAsync(harness, operationId);

            var outcome = await harness.Resolutions.RecordAsync(
                new ServicingResolutionExecution(
                    operationId,
                    ServicingResolutionKind.EscalateExternalAction,
                    "operator-9",
                    "the supplier must settle the ancillary manually",
                    generation,
                    "CASE-8801"));

            Assert.False(outcome.IsReplay);

            await using var reading = NewHarness();
            var view = (await reading.ReconciliationView.FindAsync(operationId))!;

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, view.Status);
            Assert.Equal(ServicingRecoveryAction.ManualResolutionRequired, view.RecoveryAction);
            Assert.NotEmpty(view.ManualReviewReasons);
            Assert.Single(view.ManualResolutions);
        }

        [Fact]
        public async Task R24f_An_unattributed_resolution_is_refused()
        {
            await using var harness = NewHarness();
            var operationId = await UnresolvedVoidAsync(harness);
            var generation = await GenerationAsync(harness, operationId);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Resolutions.RecordAsync(
                    new ServicingResolutionExecution(
                        operationId,
                        ServicingResolutionKind.ResumeFromCheckpoint,
                        "   ",
                        "   ",
                        generation)));

            Assert.Equal(20332, error.Code);
            Assert.Equal(422, error.HttpStatus);
            Assert.Empty(await harness.ManualResolutions.ListAsync(operationId));
        }

        [Fact]
        public async Task R24g_An_unknown_operation_is_refused()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Resolutions.RecordAsync(Resume(-1, 1, "operator-1")));

            Assert.Equal(20328, error.Code);
            Assert.Equal(404, error.HttpStatus);
        }

        private static ServicingResolutionExecution Resume(long operationId, long generation, string actor)
            => new(
                operationId,
                ServicingResolutionKind.ResumeFromCheckpoint,
                actor,
                "the issuer confirmed the mutation out of band",
                generation);

        private async Task<long> ManualReviewExchangeAsync(
            OrderSliceHarness setup,
            OrderSliceHarness harness)
        {
            var scenario = await TicketedAsync(_fixture, harness);

            await AttachAncillaryAsync(_fixture, setup, scenario.OrderId, _manual, [scenario.CouponId]);

            harness.AncillaryDispositions.DefaultDisposition = AncillaryExchangeDisposition.ManualReview;

            var outcome = await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, outcome.OperationStatus);

            return outcome.OperationId;
        }

        private async Task<long> UnresolvedVoidAsync(OrderSliceHarness harness)
        {
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();
            var key = NewKey();

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Unknown;
            harness.DocumentVoids.RecoveryOutcome = ProviderOperationOutcome.Unknown;

            await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            var reconciling = await harness.VoidDocument.VoidAsync(
                order.Id, ticket.Id, Reason, Detail, Actor, key);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, reconciling.OperationStatus);

            return reconciling.OperationId;
        }

        private static async Task<long> GenerationAsync(OrderSliceHarness harness, long operationId)
        {
            var view = await harness.ReconciliationView.FindAsync(operationId);

            Assert.NotNull(view);

            return view!.ClaimGeneration;
        }

        private async Task<IReadOnlyList<Domain.ElectronicTicketAggregate.ElectronicTicket>> TicketsAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await new ElectronicTicketRepository(command).ListByOrderAsync(orderId);
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"resolution-{Guid.NewGuid():N}");

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private static async Task<Order> TicketedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            return order;
        }
    }
}
