using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Ordering.Query.OrderAggregate.View;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ServicingReconciliationQueryTests
    {
        private const VoidReason Reason = VoidReason.AgentError;
        private const string Detail = "reconciliation evidence";
        private const long Actor = 7;

        private readonly OrderingDatabaseFixture _fixture;

        public ServicingReconciliationQueryTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task R1_A_reconciling_operation_is_identified_exactly()
        {
            await using var harness = NewHarness();
            var (order, ticket, operationId) = await UnresolvedVoidAsync(harness);

            var view = await ViewAsync(harness, operationId);

            Assert.Equal(operationId, view.OperationId);
            Assert.Equal(order.Id, view.OrderId);
            Assert.Equal(ServicingOperationKind.VoidDocument, view.Kind);
            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, view.Status);
            Assert.True(view.NeedsReconciliation);
            Assert.NotEqual(default, view.CreatedAt);
            Assert.NotEqual(default, view.UpdatedAt);
            Assert.NotNull(ticket.DocumentNumber);
        }

        [Fact]
        public async Task R2_The_unresolved_stage_is_exposed()
        {
            await using var harness = NewHarness();
            var (_, _, operationId) = await UnresolvedVoidAsync(harness);

            var view = await ViewAsync(harness, operationId);

            Assert.Equal(nameof(ServicingEvidenceStage.DocumentVoid), view.UnresolvedStage);
        }

        [Fact]
        public async Task R3_The_durable_provider_outcome_and_reference_are_exposed()
        {
            await using var harness = NewHarness();
            var (_, ticket, operationId) = await UnresolvedVoidAsync(harness);

            var view = await ViewAsync(harness, operationId);
            var evidence = Assert.Single(view.ExternalEvidence);

            Assert.Equal(ServicingEvidenceStage.DocumentVoid, evidence.Stage);
            Assert.Equal(ProviderOperationOutcome.Unknown, evidence.Outcome);
            Assert.True(evidence.IsUnresolved);
            Assert.False(evidence.IsConfirmed);
            Assert.Equal(AccountableDocumentKind.ElectronicTicket, evidence.DocumentKind);
            Assert.Equal(ticket.DocumentNumber, evidence.DocumentNumber);
            Assert.Single(view.UnresolvedEvidence);
            Assert.Empty(view.ConfirmedEvidence);
        }

        [Fact]
        public async Task R4_The_confirmed_document_truth_is_exposed()
        {
            await using var harness = NewHarness();
            var (_, ticket, operationId) = await UnresolvedVoidAsync(harness);

            var view = await ViewAsync(harness, operationId);
            var document = Assert.Single(
                view.Documents,
                candidate => candidate.DocumentId == ticket.Id);

            Assert.Equal(AccountableDocumentKind.ElectronicTicket, document.Kind);
            Assert.Equal(ticket.DocumentNumber, document.DocumentNumber);
            Assert.Equal(nameof(ElectronicTicketStatus.Issued), document.StatusSummary);
            Assert.Equal(ticket.DocumentVersion, document.DocumentVersion);
            Assert.Null(document.PredecessorElectronicTicketId);
        }

        [Fact]
        public async Task R5_The_coupon_control_status_is_exposed()
        {
            await using var harness = NewHarness();
            var (_, ticket, operationId) = await UnresolvedVoidAsync(harness);

            var view = await ViewAsync(harness, operationId);

            Assert.NotEmpty(view.ControlEvidence);
            Assert.All(
                view.ControlEvidence.Where(evidence => evidence.ElectronicTicketId == ticket.Id),
                evidence =>
                {
                    Assert.Equal(TicketCouponControlStatus.Local, evidence.ControlStatus);
                    Assert.True(evidence.IsLocallyControlled);
                });
            Assert.Empty(view.NonLocalControl);
        }

        [Fact]
        public async Task R6_Awaiting_external_is_distinguishable_from_needs_reconciliation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Unknown;

            var suspended = await harness.VoidDocument.VoidAsync(
                order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            var view = await ViewAsync(harness, suspended.OperationId);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, view.Status);
            Assert.True(view.AwaitsExternal);
            Assert.False(view.NeedsReconciliation);
            Assert.True(view.IsUnresolved);
            Assert.Equal(ServicingRecoveryAction.ReplayCommand, view.RecoveryAction);
        }

        [Fact]
        public async Task R7_A_rejected_operation_is_distinguishable_from_needs_reconciliation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Rejected;

            var rejected = await harness.VoidDocument.VoidAsync(
                order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            var view = await ViewAsync(harness, rejected.OperationId);

            Assert.Equal(ServicingOperationStatus.Rejected, view.Status);
            Assert.True(view.IsRejected);
            Assert.False(view.NeedsReconciliation);
            Assert.False(view.IsUnresolved);
            Assert.Null(view.UnresolvedStage);
            Assert.Equal(ServicingRecoveryAction.NoneRequired, view.RecoveryAction);
        }

        [Fact]
        public async Task R8_A_completed_operation_is_not_reported_as_unresolved_work()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            var completed = await harness.VoidDocument.VoidAsync(
                order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            var view = await ViewAsync(harness, completed.OperationId);

            Assert.Equal(ServicingOperationStatus.Completed, view.Status);
            Assert.True(view.IsCompleted);
            Assert.False(view.IsUnresolved);
            Assert.Null(view.UnresolvedStage);
            Assert.Equal(ServicingRecoveryAction.NoneRequired, view.RecoveryAction);
            Assert.Single(view.ConfirmedEvidence);
            Assert.Empty(view.UnresolvedEvidence);

            var unresolved = await harness.Reconciliation.ListUnresolvedAsync(order.Id);

            Assert.DoesNotContain(unresolved, snapshot => snapshot.OperationId == completed.OperationId);
        }

        [Fact]
        public async Task R8b_Only_unresolved_operations_are_listed_for_an_order()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var tickets = await TicketsAsync(order.Id);

            var completed = await harness.VoidDocument.VoidAsync(
                order.Id, tickets[0].Id, Reason, Detail, Actor, NewKey());

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Unknown;

            var suspended = await harness.VoidDocument.VoidAsync(
                order.Id, tickets[1].Id, Reason, Detail, Actor, NewKey());

            var unresolved = await harness.Reconciliation.ListUnresolvedAsync(order.Id);

            Assert.Equal([suspended.OperationId], unresolved.Select(snapshot => snapshot.OperationId));
            Assert.DoesNotContain(unresolved, snapshot => snapshot.OperationId == completed.OperationId);
        }

        [Fact]
        public async Task R9_A_non_local_coupon_stays_fail_closed_for_a_protected_action()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            await SetControlAsync(ticket.Id, TicketCouponControlStatus.External);

            await using var voiding = NewHarness();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => voiding.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey()));

            Assert.Equal(20205, error.Code);
            Assert.Empty(voiding.DocumentVoids.ObservedVoidKeys);
        }

        [Theory]
        [InlineData(TicketCouponControlStatus.External)]
        [InlineData(TicketCouponControlStatus.ReleasePending)]
        [InlineData(TicketCouponControlStatus.Unknown)]
        public async Task R10_R11_R12_A_non_local_control_status_is_visible_to_the_operator(
            TicketCouponControlStatus control)
        {
            await using var harness = NewHarness();
            var (_, ticket, operationId) = await UnresolvedVoidAsync(harness);

            await SetControlAsync(ticket.Id, control);

            await using var reading = NewHarness();
            var view = await ViewAsync(reading, operationId);

            Assert.All(
                view.ControlEvidence.Where(evidence => evidence.ElectronicTicketId == ticket.Id),
                evidence =>
                {
                    Assert.Equal(control, evidence.ControlStatus);
                    Assert.False(evidence.IsLocallyControlled);
                });

            Assert.NotEmpty(view.NonLocalControl);
        }

        [Fact]
        public async Task R13_Reconciliation_never_returns_a_coupon_to_local_control()
        {
            await using var harness = NewHarness();
            var (order, ticket, operationId) = await UnresolvedVoidAsync(harness);

            await SetControlAsync(ticket.Id, TicketCouponControlStatus.External);

            await using var reading = NewHarness();

            await reading.ReconciliationView.ComposeAsync(
                (await reading.Reconciliation.FindOperationAsync(operationId))!,
                CancellationToken.None);

            await reading.Reconciliation.ListControlAsync(order.Id);
            await reading.Reconciliation.ListDocumentsAsync(order.Id);
            await reading.Reconciliation.ListReservationsAsync(order.Id);

            var after = (await TicketsAsync(order.Id)).Single(candidate => candidate.Id == ticket.Id);

            Assert.All(
                after.Coupons,
                coupon => Assert.Equal(TicketCouponControlStatus.External, coupon.ControlStatus));
        }

        [Fact]
        public async Task R14_No_operator_resolution_can_return_external_control_to_local()
        {
            await using var harness = NewHarness();
            var (order, ticket, operationId) = await UnresolvedVoidAsync(harness);

            await SetControlAsync(ticket.Id, TicketCouponControlStatus.External);

            await using var resolving = NewHarness();
            var generation = (await resolving.Reconciliation.FindOperationAsync(operationId))!.ClaimGeneration;

            await resolving.Resolutions.RecordAsync(
                new Application.OrderAggregate.Services.Reconciliation.ServicingResolutionExecution(
                    operationId,
                    ServicingResolutionKind.ResumeFromCheckpoint,
                    "operator-1",
                    "issuer control return is not automatable",
                    generation));

            var after = (await TicketsAsync(order.Id)).Single(candidate => candidate.Id == ticket.Id);

            Assert.All(
                after.Coupons,
                coupon => Assert.Equal(TicketCouponControlStatus.External, coupon.ControlStatus));
        }

        private async Task<(Order Order, Domain.ElectronicTicketAggregate.ElectronicTicket Ticket, long OperationId)>
            UnresolvedVoidAsync(OrderSliceHarness harness)
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

            return (order, ticket, reconciling.OperationId);
        }

        private static async Task<ServicingReconciliationView> ViewAsync(
            OrderSliceHarness harness,
            long operationId)
        {
            var snapshot = await harness.Reconciliation.FindOperationAsync(operationId);

            Assert.NotNull(snapshot);

            return await harness.ReconciliationView.ComposeAsync(snapshot!, CancellationToken.None);
        }

        private async Task SetControlAsync(long ticketId, TicketCouponControlStatus control)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[TicketCoupons] SET [ControlStatus] = {0} WHERE [TicketId] = {1}",
                (int)control,
                ticketId);
        }

        private async Task<IReadOnlyList<Domain.ElectronicTicketAggregate.ElectronicTicket>> TicketsAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await new ElectronicTicketRepository(command).ListByOrderAsync(orderId);
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

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
