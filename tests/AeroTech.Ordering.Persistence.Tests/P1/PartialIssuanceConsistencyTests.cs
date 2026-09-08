using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P1
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class PartialIssuanceConsistencyTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public PartialIssuanceConsistencyTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Theory]
        [InlineData(ProviderOperationOutcome.Pending, ServicingOperationStatus.AwaitingExternal)]
        [InlineData(ProviderOperationOutcome.Unknown, ServicingOperationStatus.AwaitingExternal)]
        [InlineData(ProviderOperationOutcome.Rejected, ServicingOperationStatus.NeedsReconciliation)]
        public async Task A_confirmed_traveler_is_documented_on_the_order_even_when_a_later_traveler_does_not_confirm(
            ProviderOperationOutcome outcomeForB,
            ServicingOperationStatus expectedOperationStatus)
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            harness.Documents.OutcomeForTraveler(context.TravelerB, outcomeForB);

            var result = await harness.Issue.IssueAsync(context.OrderId, NewKey(), null);

            Assert.Equal(expectedOperationStatus, result.OperationStatus);
            Assert.Single(result.Tickets);

            await using var verification = _fixture.NewCommandContext();

            var ticket = await verification.ElectronicTickets
                .Include(candidate => candidate.Coupons)
                .AsNoTracking()
                .SingleAsync(candidate => candidate.CurrentServicingOrderId == context.OrderId);

            Assert.Equal(context.TravelerA, ticket.TravelerId);

            var order = await LoadOrderAsync(verification, context.OrderId);
            var services = order.OrderServices.Where(service => service.IsAirTransport).ToList();

            foreach (var service in services.Where(service => service.SoleBeneficiaryId == context.TravelerA))
            {
                Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus);
                Assert.Equal(ticket.Id, service.ElectronicTicketId);
                Assert.Contains(ticket.Coupons, coupon => coupon.Id == service.TicketCouponId
                                                          && coupon.OrderServiceId == service.Id);
            }

            foreach (var service in services.Where(service => service.SoleBeneficiaryId == context.TravelerB))
            {
                Assert.NotEqual(OrderServiceDocumentStatus.Issued, service.DocumentStatus);
                Assert.Null(service.ElectronicTicketId);
                Assert.Null(service.TicketCouponId);
            }

            Assert.NotNull(order.ActiveTimeLimit(TimeLimitType.Ticketing));
            Assert.NotNull(order.TimeToLive);
            Assert.False(order.IsTicketingComplete());

            var blocking = await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .AnyAsync(claim => claim.OrderId == context.OrderId && claim.IsBlocking);

            Assert.True(blocking);

            await using var query = _fixture.NewQueryContext();
            var projected = await query.Orders.AsNoTracking().SingleAsync(row => row.Id == context.OrderId);

            Assert.Equal(OrderServiceDocumentStatus.Pending, projected.DocumentSummary);
            Assert.NotEqual(OrderStatus.Ticketed, projected.Status);
        }

        [Fact]
        public async Task Recovering_the_last_traveler_completes_the_ticketing_obligation_only_then()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            harness.Documents.OutcomeForTraveler(context.TravelerB, ProviderOperationOutcome.Unknown);

            var key = NewKey();
            var first = await harness.Issue.IssueAsync(context.OrderId, key, null);

            var reservedNumber = await ReservedNumberAsync(first.OperationId, context.TravelerB);

            await using (var midway = _fixture.NewCommandContext())
            {
                var order = await LoadOrderAsync(midway, context.OrderId);
                Assert.NotNull(order.ActiveTimeLimit(TimeLimitType.Ticketing));
                Assert.False(order.IsTicketingComplete());
            }

            harness.Documents.RecoveryForTraveler(context.TravelerB, ProviderOperationOutcome.Confirmed);

            var recovered = await harness.Issue.IssueAsync(context.OrderId, key, null);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);

            await using var verification = _fixture.NewCommandContext();

            var tickets = await verification.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .AsNoTracking()
                .Where(ticket => ticket.CurrentServicingOrderId == context.OrderId)
                .ToListAsync();

            Assert.Equal(2, tickets.Count);
            Assert.Contains(tickets, ticket => ticket.DocumentNumber == reservedNumber);

            var completed = await LoadOrderAsync(verification, context.OrderId);

            Assert.True(completed.IsTicketingComplete());
            Assert.All(completed.OrderServices, service =>
                Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus));
            Assert.Null(completed.ActiveTimeLimit(TimeLimitType.Ticketing));
            Assert.Null(completed.TimeToLive);

            Assert.False(await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .AnyAsync(claim => claim.OrderId == context.OrderId && claim.IsBlocking));

            var receipt = await verification.Set<CommandReceipt>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == first.ReceiptId);

            Assert.Equal(CommandReceiptStatus.Completed, receipt.Status);

            await using var query = _fixture.NewQueryContext();
            Assert.Equal(
                OrderServiceDocumentStatus.Issued,
                (await query.Orders.AsNoTracking().SingleAsync(row => row.Id == context.OrderId)).DocumentSummary);
        }

        [Fact]
        public async Task A_stale_order_that_does_not_know_about_its_persisted_ticket_is_repaired_idempotently()
        {
            var subject = $"subject-{Guid.NewGuid():N}";

            await using var harness = NewHarness(subject);
            var context = await ReservedOrderAsync(harness);

            harness.Documents.OutcomeForTraveler(context.TravelerB, ProviderOperationOutcome.Pending);

            var key = NewKey();
            var first = await harness.Issue.IssueAsync(context.OrderId, key, null);

            var ticketsBefore = await CountAsync(command => command.ElectronicTickets
                .CountAsync(ticket => ticket.CurrentServicingOrderId == context.OrderId));
            var couponsBefore = await CountAsync(command => command.Set<TicketCoupon>().CountAsync());
            var allocationsBefore = await CountAsync(command => command.Set<DocumentStockAllocation>()
                .CountAsync(allocation => allocation.OperationId == first.OperationId));

            await StripDocumentLinkageAsync(context.OrderId);

            await using (var stale = _fixture.NewCommandContext())
            {
                var order = await LoadOrderAsync(stale, context.OrderId);
                Assert.DoesNotContain(order.OrderServices, service => service.DocumentStatus == OrderServiceDocumentStatus.Issued);
            }

            await using (var recoveringWorker = NewHarness(subject))
            {
                recoveringWorker.Documents.RecoveryForTraveler(context.TravelerB, ProviderOperationOutcome.Pending);
                await recoveringWorker.Issue.IssueAsync(context.OrderId, key, null);
            }

            await using var verification = _fixture.NewCommandContext();

            var repaired = await LoadOrderAsync(verification, context.OrderId);
            var ticket = await verification.ElectronicTickets
                .Include(candidate => candidate.Coupons)
                .AsNoTracking()
                .SingleAsync(candidate => candidate.CurrentServicingOrderId == context.OrderId);

            foreach (var service in repaired.OrderServices
                         .Where(service => service.IsAirTransport)
                         .Where(service => service.SoleBeneficiaryId == context.TravelerA))
            {
                Assert.Equal(OrderServiceDocumentStatus.Issued, service.DocumentStatus);
                Assert.Equal(ticket.Id, service.ElectronicTicketId);
            }

            Assert.Equal(ticketsBefore, await CountAsync(command => command.ElectronicTickets
                .CountAsync(t => t.CurrentServicingOrderId == context.OrderId)));
            Assert.Equal(couponsBefore, await CountAsync(command => command.Set<TicketCoupon>().CountAsync()));
            Assert.Equal(allocationsBefore, await CountAsync(command => command.Set<DocumentStockAllocation>()
                .CountAsync(allocation => allocation.OperationId == first.OperationId)));
        }

        [Fact]
        public async Task Recording_document_evidence_never_advances_the_commercial_version()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            var before = await CommercialVersionAsync(context.OrderId);

            harness.Documents.OutcomeForTraveler(context.TravelerB, ProviderOperationOutcome.Pending);

            var key = NewKey();
            await harness.Issue.IssueAsync(context.OrderId, key, null);

            Assert.Equal(before, await CommercialVersionAsync(context.OrderId));

            harness.Documents.RecoveryForTraveler(context.TravelerB, ProviderOperationOutcome.Confirmed);
            await harness.Issue.IssueAsync(context.OrderId, key, null);

            Assert.Equal(before, await CommercialVersionAsync(context.OrderId));
        }

        private async Task StripDocumentLinkageAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                """
                UPDATE [Order].[OrderServices]
                SET [DocumentStatus] = {0}, [ElectronicTicketId] = NULL, [TicketCouponId] = NULL
                WHERE [OrderId] = {1};
                """,
                (int)OrderServiceDocumentStatus.Pending,
                orderId);
        }

        private static Task<Domain.OrderAggregate.Order> LoadOrderAsync(OrderingDbContext command, long orderId)
            => command.Orders
                .Include(order => order.OrderServices).ThenInclude(service => service.Beneficiaries)
                .Include(order => order.OrderServices).ThenInclude(service => service.AirTransportDetail)
                .Include(order => order.TimeLimits)
                .AsNoTracking()
                .SingleAsync(order => order.Id == orderId);

        private async Task<int> CommercialVersionAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return (await command.Orders.AsNoTracking().SingleAsync(order => order.Id == orderId)).CommercialVersion;
        }

        private async Task<int> CountAsync(Func<OrderingDbContext, Task<int>> query)
        {
            await using var command = _fixture.NewCommandContext();

            return await query(command);
        }

        private async Task<string> ReservedNumberAsync(long operationId, long travelerId)
        {
            await using var verification = _fixture.NewCommandContext();

            return (await verification.Set<DocumentStockAllocation>()
                .AsNoTracking()
                .SingleAsync(allocation => allocation.OperationId == operationId
                                           && allocation.DocumentRole == $"Ticket:{travelerId}")).DocumentNumber;
        }

        private async Task<IssueContext> ReservedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var travelers = order.OrderServices
                .Where(service => service.IsAirTransport)
                .Select(service => service.SoleBeneficiaryId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            return new IssueContext(order.Id, travelers[0], travelers[1]);
        }

        private sealed record IssueContext(long OrderId, long TravelerA, long TravelerB);

        private OrderSliceHarness NewHarness(string? subject = null)
            => new(_fixture, TestCallerContexts.AgencyUser(11, subject ?? $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");
    }
}
