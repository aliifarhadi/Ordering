using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.DocumentVoid;
using AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class DocumentVoidFlowTests
    {
        private const VoidReason Reason = VoidReason.AgentError;
        private const string Detail = "duplicate issuance";
        private const long Actor = 7;

        private readonly OrderingDatabaseFixture _fixture;

        public DocumentVoidFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task An_unused_electronic_ticket_is_voided()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            var outcome = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            Assert.Equal(AccountableDocumentKind.ElectronicTicket, outcome.DocumentKind);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.False(outcome.RefundRequiredInstead);

            var voided = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id);

            Assert.Equal(ElectronicTicketStatus.Voided, voided.StatusSummary);
            Assert.All(voided.Coupons, c => Assert.Equal(TicketCouponFinancialStatus.Void, c.FinancialStatus));
            Assert.Equal(ticket.DocumentVersion + 1, voided.DocumentVersion);
            Assert.Equal(ticket.DocumentNumber, voided.DocumentNumber);
            Assert.NotEmpty(voided.PriceLinks);
        }

        [Fact]
        public async Task A_coupon_under_external_control_is_refused_before_provider_execution()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            await SetCouponAsync(ticket.Id, control: TicketCouponControlStatus.External);

            await using var voiding = NewHarness();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => voiding.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey()));

            Assert.Equal(2911, error.Code);
            Assert.Empty(voiding.DocumentVoids.ObservedEligibilityKeys);
            Assert.Empty(voiding.DocumentVoids.ObservedVoidKeys);

            Assert.Equal(
                ElectronicTicketStatus.Issued,
                (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).StatusSummary);
        }

        [Fact]
        public async Task A_used_coupon_is_refused_before_provider_execution()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            await SetCouponAsync(ticket.Id, financial: TicketCouponFinancialStatus.Used);

            await using var voiding = NewHarness();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => voiding.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey()));

            Assert.Equal(2910, error.Code);
            Assert.Empty(voiding.DocumentVoids.ObservedVoidKeys);
        }

        [Fact]
        public async Task An_issuer_that_refuses_the_void_identifies_the_refund_path()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            harness.DocumentVoids.Eligibility = EligibilityOutcome.Denied;
            harness.DocumentVoids.RefundRequiredInstead = true;

            var outcome = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            Assert.True(outcome.RefundRequiredInstead);
            Assert.Equal(ServicingOperationStatus.Rejected, outcome.OperationStatus);
            Assert.Empty(harness.DocumentVoids.ObservedVoidKeys);

            Assert.Equal(
                ElectronicTicketStatus.Issued,
                (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).StatusSummary);
        }

        [Fact]
        public async Task An_elapsed_void_deadline_refuses_before_provider_execution()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            await using (var command = _fixture.NewCommandContext())
            {
                await command.Database.ExecuteSqlRawAsync(
                    "UPDATE [Order].[ElectronicTickets] SET [VoidDeadline] = '2000-01-01T00:00:00+00:00' WHERE [Id] = {0}",
                    ticket.Id);
            }

            await using var voiding = NewHarness();

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => voiding.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey()));

            Assert.Equal(2912, error.Code);
            Assert.Empty(voiding.DocumentVoids.ObservedVoidKeys);
        }

        [Fact]
        public async Task An_unknown_void_stays_reconcilable_and_recovers_once_under_the_same_key()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();
            var key = NewKey();

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            Assert.Equal(ServicingOperationStatus.AwaitingExternal, first.OperationStatus);
            Assert.Equal(
                ElectronicTicketStatus.Issued,
                (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).StatusSummary);

            var voidCalls = harness.DocumentVoids.ObservedVoidKeys.Count;

            harness.DocumentVoids.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);
            Assert.Equal(voidCalls, harness.DocumentVoids.ObservedVoidKeys.Count);
            Assert.Equal(
                harness.DocumentVoids.ObservedVoidKeys.Distinct(),
                harness.DocumentVoids.ObservedRecoveryKeys.Distinct());

            Assert.Equal(
                ElectronicTicketStatus.Voided,
                (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).StatusSummary);
        }

        [Fact]
        public async Task A_replay_of_a_completed_void_changes_nothing()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();
            var key = NewKey();

            var first = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);
            var voidCalls = harness.DocumentVoids.ObservedVoidKeys.Count;
            var versionAfterFirst = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).DocumentVersion;

            var replay = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            Assert.True(replay.IsReplay);
            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(voidCalls, harness.DocumentVoids.ObservedVoidKeys.Count);
            Assert.Equal(
                versionAfterFirst,
                (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).DocumentVersion);
        }

        [Fact]
        public async Task Voiding_the_ticket_does_not_void_the_miscellaneous_document()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderWithEmdAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            var document = (await DocumentsAsync(order.Id)).Single();

            Assert.Equal(ElectronicMiscDocumentStatus.Issued, document.StatusSummary);
            Assert.All(document.Coupons, c => Assert.Equal(EmdCouponStatus.OpenForUse, c.Status));
        }

        [Fact]
        public async Task A_miscellaneous_document_is_voided_independently()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderWithEmdAsync(harness);
            var document = (await DocumentsAsync(order.Id)).Single();

            var outcome = await harness.VoidDocument.VoidAsync(order.Id, document.Id, Reason, Detail, Actor, NewKey());

            Assert.Equal(AccountableDocumentKind.ElectronicMiscDocument, outcome.DocumentKind);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            var voided = (await DocumentsAsync(order.Id)).Single();

            Assert.Equal(ElectronicMiscDocumentStatus.Voided, voided.StatusSummary);
            Assert.All(voided.Coupons, c => Assert.Equal(EmdCouponStatus.Void, c.Status));
            Assert.Equal(document.DocumentVersion + 1, voided.DocumentVersion);
        }

        [Fact]
        public async Task Voiding_the_miscellaneous_document_does_not_mutate_the_ticket()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderWithEmdAsync(harness);
            var document = (await DocumentsAsync(order.Id)).Single();
            var before = (await TicketsAsync(order.Id)).Select(t => (t.Id, t.StatusSummary, t.DocumentVersion)).ToList();

            await harness.VoidDocument.VoidAsync(order.Id, document.Id, Reason, Detail, Actor, NewKey());

            var after = (await TicketsAsync(order.Id)).Select(t => (t.Id, t.StatusSummary, t.DocumentVersion)).ToList();

            Assert.Equal(before, after);
        }

        [Fact]
        public void The_production_void_command_reaches_the_new_document_rail()
        {
            var dependencies = typeof(VoidTrafficDocumentCommandHandler)
                .GetConstructors()
                .Single()
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToList();

            Assert.Contains(typeof(IDocumentVoidService), dependencies);
            Assert.DoesNotContain(
                dependencies,
                type => type.Name.Contains("TrafficDocument", StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_void_commits_no_pricing_change_and_no_commercial_version_move()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            var before = await ReloadAsync(order.Id);

            harness.Events.Dispatched.Clear();

            await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            var after = await ReloadAsync(order.Id);

            Assert.Equal(before.CommercialVersion, after.CommercialVersion);
            Assert.Equal(before.FinancialSequence, after.FinancialSequence);
            Assert.Equal(before.ObligationVersion, after.ObligationVersion);
            Assert.Equal(before.CustomerTotal, after.CustomerTotal);
            Assert.Equal(before.PriceChangeSets.Count, after.PriceChangeSets.Count);
            Assert.Equal(before.PricingLines.Count, after.PricingLines.Count);
            Assert.Equal(before.Changes.Count, after.Changes.Count);

            Assert.DoesNotContain(
                harness.Events.Dispatched,
                domainEvent => domainEvent.GetType().Name is "OrderCancelled" or "OrderPricingChanged");

            Assert.All(
                after.OrderServices.Where(s => ticket.VoidedServiceIds().Contains(s.Id)),
                service =>
                {
                    Assert.Equal(OrderServiceDocumentStatus.Voided, service.DocumentStatus);
                    Assert.NotEqual(OrderServiceStatus.Cancelled, service.Status);
                });
        }

        [Fact]
        public async Task The_order_view_shows_the_document_as_voided()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            await using var query = _fixture.NewQueryContext();

            var details = await query.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == order.Id);

            var view = System.Text.Json.JsonSerializer.Deserialize<Query.OrderAggregate.View.OrderView>(
                details.SnapshotJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            var projected = view.ElectronicTickets.Single(candidate => candidate.TicketId == ticket.Id);

            Assert.Equal(ElectronicTicketStatus.Voided, projected.Status);
            Assert.All(projected.Coupons, coupon => Assert.Equal(TicketCouponFinancialStatus.Void, coupon.FinancialStatus));
            Assert.NotEqual(OrderStatus.Cancelled, view.Status);
        }

        [Fact]
        public async Task A_confirmed_ticket_void_stores_its_provenance()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            var issuanceOperationId = ticket.OperationId;
            var issuanceProviderReference = ticket.ProviderReference;

            var outcome = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            var voided = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id);
            var record = voided.VoidRecord;

            Assert.NotNull(record);
            Assert.Equal(outcome.OperationId, record!.OperationId);
            Assert.Equal(Reason, record.Reason);
            Assert.Equal(Detail, record.ReasonDetail);
            Assert.Equal(Actor, record.VoidedBy);
            Assert.NotEqual(default, record.VoidedAt);
            Assert.Equal($"VOID-{voided.DocumentNumber}", record.ProviderReference);

            Assert.Equal(issuanceOperationId, voided.OperationId);
            Assert.Equal(issuanceProviderReference, voided.ProviderReference);
            Assert.NotEqual(issuanceOperationId, record.OperationId);
        }

        [Fact]
        public async Task A_confirmed_miscellaneous_document_void_stores_its_provenance()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderWithEmdAsync(harness);
            var document = (await DocumentsAsync(order.Id)).Single();

            var issuanceOperationId = document.OperationId;
            var issuanceProviderReference = document.ProviderReference;

            var outcome = await harness.VoidDocument.VoidAsync(order.Id, document.Id, Reason, Detail, Actor, NewKey());

            var voided = (await DocumentsAsync(order.Id)).Single();
            var record = voided.VoidRecord;

            Assert.NotNull(record);
            Assert.Equal(outcome.OperationId, record!.OperationId);
            Assert.Equal(Reason, record.Reason);
            Assert.Equal(Detail, record.ReasonDetail);
            Assert.Equal(Actor, record.VoidedBy);
            Assert.NotEqual(default, record.VoidedAt);

            Assert.Equal(issuanceOperationId, voided.OperationId);
            Assert.Equal(issuanceProviderReference, voided.ProviderReference);
        }

        [Fact]
        public async Task A_refused_or_uncertain_void_stores_no_provenance()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Rejected;

            await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            Assert.Null((await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).VoidRecord);

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Unknown;

            await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, NewKey());

            var after = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id);

            Assert.Null(after.VoidRecord);
            Assert.Equal(ElectronicTicketStatus.Issued, after.StatusSummary);
        }

        [Fact]
        public async Task A_recovered_void_records_provenance_once_under_the_original_operation()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();
            var key = NewKey();

            harness.DocumentVoids.VoidOutcome = ProviderOperationOutcome.Unknown;

            var first = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            Assert.Null((await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).VoidRecord);

            harness.DocumentVoids.RecoveryOutcome = ProviderOperationOutcome.Confirmed;

            var recovered = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            var voided = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Equal(first.OperationId, voided.VoidRecord!.OperationId);
            Assert.Equal(Reason, voided.VoidRecord.Reason);

            var versionAfterRecovery = voided.DocumentVersion;

            var again = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            var settled = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id);

            Assert.True(again.IsReplay);
            Assert.Equal(versionAfterRecovery, settled.DocumentVersion);
            Assert.Equal(first.OperationId, settled.VoidRecord!.OperationId);
        }

        [Fact]
        public async Task A_replay_cannot_change_the_recorded_reason()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();
            var key = NewKey();

            await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            var recorded = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).VoidRecord!;

            var replay = await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            var after = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).VoidRecord!;

            Assert.True(replay.IsReplay);
            Assert.Equal(recorded.OperationId, after.OperationId);
            Assert.Equal(recorded.Reason, after.Reason);
            Assert.Equal(recorded.ReasonDetail, after.ReasonDetail);
            Assert.Equal(recorded.VoidedAt, after.VoidedAt);
            Assert.Equal(recorded.VoidedBy, after.VoidedBy);
        }

        [Fact]
        public async Task The_same_key_with_a_different_reason_conflicts()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = (await TicketsAsync(order.Id)).First();
            var key = NewKey();

            await harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, Detail, Actor, key);

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.VoidDocument.VoidAsync(order.Id, ticket.Id, VoidReason.Duplicate, Detail, Actor, key));

            await Assert.ThrowsAsync<BusinessException>(
                () => harness.VoidDocument.VoidAsync(order.Id, ticket.Id, Reason, "something else", Actor, key));

            var record = (await TicketsAsync(order.Id)).Single(t => t.Id == ticket.Id).VoidRecord!;

            Assert.Equal(Reason, record.Reason);
            Assert.Equal(Detail, record.ReasonDetail);
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

        private static async Task<Order> TicketedOrderWithEmdAsync(OrderSliceHarness harness)
        {
            var order = await TicketedOrderAsync(harness);

            harness.Quotes.Quote(ProductAdditionFactory.EmdBaggage(order));

            await harness.OrderChange.AddServiceAsync(
                order.Id,
                [new Application.OrderAggregate.Services.OrderChange.SelectedQuotedOffer(
                    ProductAdditionFactory.QuotedOfferId,
                    [ProductAdditionFactory.SelectedOfferItemId])],
                NewKey(),
                order.CommercialVersion);

            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            return order;
        }

        private async Task SetCouponAsync(
            long ticketId,
            TicketCouponControlStatus? control = null,
            TicketCouponFinancialStatus? financial = null)
        {
            await using var command = _fixture.NewCommandContext();

            if (control is { } controlStatus)
                await command.Database.ExecuteSqlRawAsync(
                    "UPDATE [Order].[TicketCoupons] SET [ControlStatus] = {0} WHERE [TicketId] = {1}",
                    (int)controlStatus,
                    ticketId);

            if (financial is { } financialStatus)
                await command.Database.ExecuteSqlRawAsync(
                    "UPDATE [Order].[TicketCoupons] SET [FinancialStatus] = {0} WHERE [TicketId] = {1}",
                    (int)financialStatus,
                    ticketId);
        }

        private async Task<IReadOnlyList<Domain.ElectronicTicketAggregate.ElectronicTicket>> TicketsAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await new ElectronicTicketRepository(command).ListByOrderAsync(orderId);
        }

        private async Task<IReadOnlyList<Domain.ElectronicMiscDocumentAggregate.ElectronicMiscDocument>> DocumentsAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await new ElectronicMiscDocumentRepository(command).ListByOrderAsync(orderId);
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return (await new OrderRepository(context).GetAsync(orderId))!;
        }
    }
}
