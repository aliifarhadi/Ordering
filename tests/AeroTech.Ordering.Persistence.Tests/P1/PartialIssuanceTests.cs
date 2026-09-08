using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Persistence.Operations;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P1
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class PartialIssuanceTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public PartialIssuanceTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task Both_travelers_confirmed_completes_the_operation_and_the_receipt()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            var result = await harness.Issue.IssueAsync(context.OrderId, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Confirmed, result.Outcome);
            Assert.Equal(ServicingOperationStatus.Completed, result.OperationStatus);
            Assert.Equal(2, result.Tickets.Count);
            Assert.Empty(result.OutstandingServiceIds);

            await AssertOperationAsync(result.OperationId, ServicingOperationStatus.Completed);
            await AssertReceiptAsync(result.ReceiptId, CommandReceiptStatus.Completed);
            await AssertClaimBlockingAsync(context.OrderId, expected: false);
        }

        [Fact]
        public async Task A_confirmed_and_b_pending_is_never_reported_as_confirmed()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            harness.Documents.OutcomeForTraveler(context.TravelerB, ProviderOperationOutcome.Pending);

            var result = await harness.Issue.IssueAsync(context.OrderId, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Pending, result.Outcome);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, result.OperationStatus);
            Assert.Single(result.Tickets);
            Assert.Equal(context.TravelerA, result.Tickets[0].TravelerId);
            Assert.NotEmpty(result.OutstandingServiceIds);

            await AssertOperationAsync(result.OperationId, ServicingOperationStatus.AwaitingExternal);
            await AssertReceiptAsync(result.ReceiptId, CommandReceiptStatus.Pending);
            await AssertClaimBlockingAsync(context.OrderId, expected: true);
        }

        [Fact]
        public async Task A_retry_after_a_pending_traveler_recovers_only_that_traveler_on_the_same_number()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            harness.Documents.OutcomeForTraveler(context.TravelerB, ProviderOperationOutcome.Pending);

            var key = NewKey();
            var first = await harness.Issue.IssueAsync(context.OrderId, key, null);

            var reservedNumber = await ReservedNumberAsync(first.OperationId, context.TravelerB);
            var issueRequestsBefore = harness.Documents.Requests.Count;

            harness.Documents.RecoveryForTraveler(context.TravelerB, ProviderOperationOutcome.Pending);

            var retry = await harness.Issue.IssueAsync(context.OrderId, key, null);

            Assert.Equal(first.OperationId, retry.OperationId);
            Assert.Equal(first.ReceiptId, retry.ReceiptId);
            Assert.NotEqual(ProviderOperationOutcome.Confirmed, retry.Outcome);
            Assert.Equal(ServicingOperationStatus.AwaitingExternal, retry.OperationStatus);

            Assert.Equal(issueRequestsBefore, harness.Documents.Requests.Count);
            Assert.Single(harness.Documents.Recoveries);
            Assert.Equal(reservedNumber, harness.Documents.Recoveries[0].DocumentNumber);

            Assert.Equal(reservedNumber, await ReservedNumberAsync(first.OperationId, context.TravelerB));

            await using var verification = _fixture.NewCommandContext();
            Assert.Equal(1, await verification.ElectronicTickets
                .CountAsync(ticket => ticket.CurrentServicingOrderId == context.OrderId && ticket.TravelerId == context.TravelerA));
        }

        [Fact]
        public async Task Recovery_of_an_unknown_traveler_completes_the_same_operation()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            harness.Documents.OutcomeForTraveler(context.TravelerB, ProviderOperationOutcome.Unknown);

            var key = NewKey();
            var first = await harness.Issue.IssueAsync(context.OrderId, key, null);

            Assert.Equal(ProviderOperationOutcome.Unknown, first.Outcome);
            await AssertReceiptAsync(first.ReceiptId, CommandReceiptStatus.Unknown);
            await AssertClaimBlockingAsync(context.OrderId, expected: true);

            var reservedNumber = await ReservedNumberAsync(first.OperationId, context.TravelerB);

            harness.Documents.RecoveryForTraveler(context.TravelerB, ProviderOperationOutcome.Confirmed);

            var recovered = await harness.Issue.IssueAsync(context.OrderId, key, null);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.Equal(first.ReceiptId, recovered.ReceiptId);
            Assert.Equal(ProviderOperationOutcome.Confirmed, recovered.Outcome);
            Assert.Equal(ServicingOperationStatus.Completed, recovered.OperationStatus);

            await AssertOperationAsync(first.OperationId, ServicingOperationStatus.Completed);
            await AssertReceiptAsync(first.ReceiptId, CommandReceiptStatus.Completed);
            await AssertClaimBlockingAsync(context.OrderId, expected: false);

            await using var verification = _fixture.NewCommandContext();

            var tickets = await verification.ElectronicTickets
                .Where(ticket => ticket.CurrentServicingOrderId == context.OrderId)
                .ToListAsync();

            Assert.Equal(2, tickets.Count);
            Assert.Single(tickets, ticket => ticket.TravelerId == context.TravelerA);
            Assert.Contains(tickets, ticket => ticket.DocumentNumber == reservedNumber);
        }

        [Fact]
        public async Task A_confirmed_and_b_rejected_needs_reconciliation_and_keeps_the_claim()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            harness.Documents.OutcomeForTraveler(context.TravelerB, ProviderOperationOutcome.Rejected);

            var result = await harness.Issue.IssueAsync(context.OrderId, NewKey(), null);

            Assert.Equal(ServicingOperationStatus.NeedsReconciliation, result.OperationStatus);
            Assert.Single(result.Tickets);

            await AssertOperationAsync(result.OperationId, ServicingOperationStatus.NeedsReconciliation);
            await AssertReceiptAsync(result.ReceiptId, CommandReceiptStatus.NeedsReconciliation);
            await AssertClaimBlockingAsync(context.OrderId, expected: true);

            await using var verification = _fixture.NewCommandContext();

            Assert.Equal(1, await verification.ElectronicTickets
                .CountAsync(ticket => ticket.CurrentServicingOrderId == context.OrderId));
            Assert.Equal(ElectronicTicketStatus.Issued, (await verification.ElectronicTickets
                .SingleAsync(ticket => ticket.CurrentServicingOrderId == context.OrderId)).StatusSummary);
        }

        [Fact]
        public async Task A_rejection_before_any_document_succeeds_terminates_safely()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            harness.Documents.Outcome = ProviderOperationOutcome.Rejected;

            var result = await harness.Issue.IssueAsync(context.OrderId, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Rejected, result.Outcome);
            Assert.Equal(ServicingOperationStatus.Rejected, result.OperationStatus);
            Assert.Empty(result.Tickets);

            await AssertOperationAsync(result.OperationId, ServicingOperationStatus.Rejected);
            await AssertReceiptAsync(result.ReceiptId, CommandReceiptStatus.Rejected);
            await AssertClaimBlockingAsync(context.OrderId, expected: false);

            await using var verification = _fixture.NewCommandContext();

            var allocations = await verification.Set<DocumentStockAllocation>()
                .AsNoTracking()
                .Where(allocation => allocation.OperationId == result.OperationId)
                .ToListAsync();

            Assert.Single(allocations);
            Assert.Equal(StockNumberState.Retired, allocations[0].State);
            Assert.Equal(0, await verification.ElectronicTickets
                .CountAsync(ticket => ticket.CurrentServicingOrderId == context.OrderId));
        }

        [Fact]
        public async Task A_stale_claim_generation_cannot_finalize_a_recovered_operation()
        {
            await using var harness = NewHarness();
            var context = await ReservedOrderAsync(harness);

            harness.Documents.OutcomeForTraveler(context.TravelerB, ProviderOperationOutcome.Unknown);
            harness.Documents.RecoveryForTraveler(context.TravelerB, ProviderOperationOutcome.Unknown);

            var key = NewKey();
            var first = await harness.Issue.IssueAsync(context.OrderId, key, null);

            var staleGeneration = await CurrentGenerationAsync(context.OrderId);

            var recovered = await harness.Issue.IssueAsync(context.OrderId, key, null);
            var currentGeneration = await CurrentGenerationAsync(context.OrderId);

            Assert.Equal(first.OperationId, recovered.OperationId);
            Assert.True(currentGeneration > staleGeneration);

            await using var command = _fixture.NewCommandContext();
            var claims = new OperationClaimStore(command, harness.Ids, new OrderingDatabaseFixture.FixedClock());

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => claims.EnsureCurrentGenerationAsync(context.OrderId, first.OperationId, staleGeneration));

            Assert.Equal(2702, error.Code);

            await claims.EnsureCurrentGenerationAsync(context.OrderId, first.OperationId, currentGeneration);
        }

        [Fact]
        public async Task A_trusted_owner_airline_beyond_int_range_is_never_narrowed_to_zero()
        {
            const long largeAirlineId = (long)int.MaxValue + 4242;

            await using var harness = NewHarness();

            try
            {
                await harness.SeedPlatformAsync(homeAirlineId: largeAirlineId);
                await AssertLargeAirlineIssuanceAsync(harness, largeAirlineId);
            }
            finally
            {
                await harness.SeedPlatformAsync();
            }
        }

        private async Task AssertLargeAirlineIssuanceAsync(OrderSliceHarness harness, long largeAirlineId)
        {
            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var result = await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Confirmed, result.Outcome);

            await using var verification = _fixture.NewCommandContext();

            var tickets = await verification.ElectronicTickets
                .AsNoTracking()
                .Where(ticket => ticket.CurrentServicingOrderId == order.Id)
                .ToListAsync();

            Assert.NotEmpty(tickets);
            Assert.All(tickets, ticket =>
            {
                Assert.NotEqual(0, ticket.IssuerCarrierId);
                Assert.Equal(largeAirlineId, ticket.IssuerCarrierId);
            });

            Assert.All(harness.Documents.Requests, request => Assert.Equal(largeAirlineId, request.IssuerCarrierId));
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

        private async Task<string> ReservedNumberAsync(long operationId, long travelerId)
        {
            await using var verification = _fixture.NewCommandContext();

            var allocation = await verification.Set<DocumentStockAllocation>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.OperationId == operationId && candidate.DocumentRole == $"Ticket:{travelerId}");

            Assert.Equal(StockNumberState.Reserved, allocation.State);

            return allocation.DocumentNumber;
        }

        private async Task<long> CurrentGenerationAsync(long orderId)
        {
            await using var verification = _fixture.NewCommandContext();

            return (await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .SingleAsync(claim => claim.OrderId == orderId && claim.IsBlocking)).Generation;
        }

        private async Task AssertOperationAsync(long operationId, ServicingOperationStatus expected)
        {
            await using var verification = _fixture.NewCommandContext();

            var operation = await verification.Set<ServicingOperation>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == operationId);

            Assert.Equal(expected, operation.Status);
        }

        private async Task AssertReceiptAsync(long receiptId, CommandReceiptStatus expected)
        {
            await using var verification = _fixture.NewCommandContext();

            var receipt = await verification.Set<CommandReceipt>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.Id == receiptId);

            Assert.Equal(expected, receipt.Status);
        }

        private async Task AssertClaimBlockingAsync(long orderId, bool expected)
        {
            await using var verification = _fixture.NewCommandContext();

            var blocking = await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .AnyAsync(claim => claim.OrderId == orderId && claim.IsBlocking);

            Assert.Equal(expected, blocking);
        }

        private sealed record IssueContext(long OrderId, long TravelerA, long TravelerB);

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");
    }
}
