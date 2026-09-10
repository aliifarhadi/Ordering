using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Persistence.Servicing;
using AeroTech.Ordering.Persistence.Tests._Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P1
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class OrderVerticalSliceTests
    {
        private readonly OrderingDatabaseFixture _fixture;

        public OrderVerticalSliceTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task The_full_slice_creates_reserves_verifies_funding_issues_and_redisplays()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            var reserved = await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            Assert.Equal(FulfillmentReservationStatus.Confirmed, reserved.ReservationStatus);
            Assert.Equal(1, reserved.CommercialVersion);

            var issued = await harness.Issue.IssueAsync(order.Id, NewKey(), null);
            Assert.Equal(ProviderOperationOutcome.Confirmed, issued.Outcome);
            Assert.Equal(2, issued.Tickets.Count);
            Assert.Equal(1, issued.CommercialVersion);

            await using var verification = _fixture.NewCommandContext();
            var tickets = await verification.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .Where(ticket => ticket.CurrentServicingOrderId == order.Id)
                .ToListAsync();

            Assert.Equal(2, tickets.Count);
            Assert.All(tickets, ticket => Assert.Equal(2, ticket.Coupons.Count));

            await using var queryVerification = _fixture.NewQueryContext();
            var details = await queryVerification.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == order.Id);

            Assert.Contains("\"DocumentNumber\"", details.SnapshotJson);
            Assert.True(details.ProjectionRevision >= 3);
        }

        [Fact]
        public async Task Each_coupon_maps_to_the_correct_traveler_service_and_sold_segment()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            await using var verification = _fixture.NewCommandContext();

            var tickets = await verification.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .Where(ticket => ticket.CurrentServicingOrderId == order.Id)
                .ToListAsync();

            var services = order.OrderServices
                .OfType<Domain.OrderAggregate.Entities.OrderService>()
                .ToDictionary(service => service.Id);

            foreach (var ticket in tickets)
            {
                foreach (var coupon in ticket.Coupons)
                {
                    var service = services[coupon.OrderServiceId];

                    Assert.Equal(ticket.TravelerId, service.SoleBeneficiaryId);
                    Assert.Equal(service.SoldSegmentId!.Value, coupon.JourneySegmentId);
                }
            }

            Assert.Equal(
                services.Values.Select(service => service.SoleBeneficiaryId).Distinct().Count(),
                tickets.Count);
        }

        [Fact]
        public async Task Issue_time_price_attribution_is_retained_on_the_document()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            await using var verification = _fixture.NewCommandContext();

            var tickets = await verification.ElectronicTickets
                .Include(ticket => ticket.Coupons)
                .Include(ticket => ticket.PriceLinks)
                .Where(ticket => ticket.CurrentServicingOrderId == order.Id)
                .ToListAsync();

            Assert.All(tickets, ticket =>
            {
                Assert.NotEmpty(ticket.PriceLinks);
                Assert.Equal(ticket.Coupons.Sum(coupon => coupon.IssuanceValue), ticket.IssuedTotal);
            });

            Assert.Equal(order.Amount.GrandTotal, tickets.Sum(ticket => ticket.IssuedTotal));
        }

        [Fact]
        public async Task A_retried_issue_with_the_same_key_resumes_the_same_operation_and_issues_no_second_ticket()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var key = NewKey();
            var first = await harness.Issue.IssueAsync(order.Id, key, null);
            var replay = await harness.Issue.IssueAsync(order.Id, key, null);

            Assert.Equal(first.OperationId, replay.OperationId);
            Assert.Equal(
                first.Tickets.Select(ticket => ticket.DocumentNumber).OrderBy(number => number),
                replay.Tickets.Select(ticket => ticket.DocumentNumber).OrderBy(number => number));

            await using var verification = _fixture.NewCommandContext();

            Assert.Equal(2, await verification.ElectronicTickets.CountAsync(ticket => ticket.CurrentServicingOrderId == order.Id));
            Assert.Equal(2, harness.Documents.Requests.Count);
        }

        [Fact]
        public async Task An_unknown_document_outcome_leaves_the_operation_unresolved_and_allocates_no_replacement_number()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            harness.Documents.Outcome = ProviderOperationOutcome.Unknown;

            var result = await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            Assert.Equal(ProviderOperationOutcome.Unknown, result.Outcome);
            Assert.Empty(result.Tickets);

            await using var verification = _fixture.NewCommandContext();

            Assert.Equal(0, await verification.ElectronicTickets.CountAsync(ticket => ticket.CurrentServicingOrderId == order.Id));

            var claim = await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .SingleAsync(candidate => candidate.OrderId == order.Id && candidate.OperationId == result.OperationId);

            Assert.True(claim.IsBlocking);

            var allocations = await verification.Set<DocumentStockAllocation>()
                .AsNoTracking()
                .Where(allocation => allocation.OperationId == result.OperationId)
                .ToListAsync();

            Assert.Single(allocations);
            Assert.Equal(StockNumberState.Reserved, allocations[0].State);
        }

        [Theory]
        [InlineData(FundingCoverageOutcome.Insufficient)]
        [InlineData(FundingCoverageOutcome.Pending)]
        [InlineData(FundingCoverageOutcome.Unknown)]
        public async Task Funding_that_is_not_confirmed_blocks_issue(FundingCoverageOutcome outcome)
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            harness.Funding.Outcome = outcome;

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Issue.IssueAsync(order.Id, NewKey(), null));

            Assert.Equal(2729, error.Code);

            await using var verification = _fixture.NewCommandContext();
            Assert.Equal(0, await verification.ElectronicTickets.CountAsync(ticket => ticket.CurrentServicingOrderId == order.Id));
        }

        [Theory]
        [InlineData(ReservationMemberStatus.Waitlisted)]
        [InlineData(ReservationMemberStatus.Rejected)]
        public async Task A_reservation_that_is_not_confirmed_blocks_issue(ReservationMemberStatus status)
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            harness.Reservation.DefaultMemberStatus = status;

            var order = await harness.CreateOrderAsync();
            var reserved = await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            Assert.NotEqual(FulfillmentReservationStatus.Confirmed, reserved.ReservationStatus);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Issue.IssueAsync(order.Id, NewKey(), null));

            Assert.Equal(2729, error.Code);
        }

        [Fact]
        public async Task An_unknown_reservation_outcome_keeps_its_claim_and_blocks_issue()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            harness.Reservation.DefaultMemberStatus = ReservationMemberStatus.Unknown;
            harness.Reservation.DefaultOutcome = ProviderOperationOutcome.Unknown;

            var order = await harness.CreateOrderAsync();
            var reserved = await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            Assert.Equal(FulfillmentReservationStatus.Unknown, reserved.ReservationStatus);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Issue.IssueAsync(order.Id, NewKey(), null));

            Assert.Equal(2700, error.Code);

            await using var verification = _fixture.NewCommandContext();

            Assert.True(await verification.Set<OperationOrderClaim>()
                .AsNoTracking()
                .AnyAsync(claim => claim.OrderId == order.Id && claim.IsBlocking));
            Assert.Equal(0, await verification.ElectronicTickets.CountAsync(ticket => ticket.CurrentServicingOrderId == order.Id));
        }

        [Fact]
        public async Task Reservation_confirmation_does_not_advance_the_commercial_version_but_updates_the_projection()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            await using (var afterCreate = _fixture.NewQueryContext())
            {
                var row = await afterCreate.Orders.AsNoTracking().SingleAsync(candidate => candidate.Id == order.Id);
                Assert.Equal(1, row.CommercialVersion);
                Assert.Equal(CommercialSummary.Active, row.CommercialSummary);
                Assert.Null(row.ReservationSummary);
            }

            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            await using var afterReserve = _fixture.NewQueryContext();
            var projected = await afterReserve.Orders.AsNoTracking().SingleAsync(candidate => candidate.Id == order.Id);

            Assert.Equal(1, projected.CommercialVersion);
            Assert.Equal(FulfillmentReservationStatus.Confirmed, projected.ReservationSummary);
            Assert.True(projected.ProjectionRevision > 1);
        }

        [Fact]
        public async Task Pre_ticket_withdrawal_releases_the_reservation_and_creates_no_refund()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            var withdrawn = await harness.Withdraw.WithdrawAsync(order.Id, VoidReason.CustomerRequest, NewKey(), null);

            Assert.Equal(CommercialSummary.Cancelled, withdrawn.CommercialSummary);
            Assert.Equal(2, withdrawn.CommercialVersion);
            Assert.Equal(ProviderOperationOutcome.Confirmed, withdrawn.ReservationReleaseOutcome);

            await using var verification = _fixture.NewCommandContext();

            Assert.Equal(0, await verification.ElectronicTickets.CountAsync(ticket => ticket.CurrentServicingOrderId == order.Id));

            var reservations = await verification.FulfillmentReservations
                .AsNoTracking()
                .Where(reservation => reservation.OrderId == order.Id)
                .ToListAsync();

            Assert.All(reservations, reservation => Assert.Equal(FulfillmentReservationStatus.Released, reservation.Status));
        }

        [Fact]
        public async Task Withdrawal_is_refused_once_the_order_is_ticketed()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Withdraw.WithdrawAsync(order.Id, VoidReason.CustomerRequest, NewKey(), null));

            Assert.Equal(2729, error.Code);
        }

        [Fact]
        public async Task A_conflicting_operation_is_blocked_while_an_irreversible_one_is_unresolved()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);

            harness.Documents.Outcome = ProviderOperationOutcome.Unknown;
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var error = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Withdraw.WithdrawAsync(order.Id, VoidReason.CustomerRequest, NewKey(), null));

            Assert.Equal(2700, error.Code);
        }

        [Fact]
        public async Task Get_order_redisplays_from_the_local_projection_without_upstream_calls()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();
            await harness.Reserve.ReserveAsync(order.Id, NewKey(), null);
            await harness.Issue.IssueAsync(order.Id, NewKey(), null);

            var reservationCallsBefore = harness.Reservation.ObservedOperationKeys.Count;
            var fundingCallsBefore = harness.Funding.ObservedOperationKeys.Count;
            var documentCallsBefore = harness.Documents.Requests.Count;

            await using var query = _fixture.NewQueryContext();
            var details = await query.OrderDetails.AsNoTracking().SingleAsync(row => row.Id == order.Id);

            Assert.False(string.IsNullOrWhiteSpace(details.SnapshotJson));
            Assert.Equal(reservationCallsBefore, harness.Reservation.ObservedOperationKeys.Count);
            Assert.Equal(fundingCallsBefore, harness.Funding.ObservedOperationKeys.Count);
            Assert.Equal(documentCallsBefore, harness.Documents.Requests.Count);
        }

        [Fact]
        public async Task The_provider_key_derives_from_the_operation_not_from_the_attempt_count()
        {
            await using var harness = NewHarness();
            await harness.SeedPlatformAsync();

            var order = await harness.CreateOrderAsync();

            var key = NewKey();
            var first = await harness.Reserve.ReserveAsync(order.Id, key, null);

            var observed = harness.Reservation.ObservedOperationKeys.ToList();

            Assert.All(observed, operationKey => Assert.Equal($"reserve:{first.OperationId}", operationKey));
        }

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");
    }
}
