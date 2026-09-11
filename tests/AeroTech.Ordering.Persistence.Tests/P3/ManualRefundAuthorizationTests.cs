using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Messages.Shared.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class ManualRefundAuthorizationTests
    {
        private const string AuthorityReference = "GOODWILL-2026-200";
        private const string Reason = "Schedule disruption goodwill settlement";
        private const string Disposition = "OriginalFormOfPayment";
        private const decimal ApprovedAmount = 500_000m;

        private readonly OrderingDatabaseFixture _fixture;

        public ManualRefundAuthorizationTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task An_airline_backoffice_actor_without_an_affirmative_decision_is_refused()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(ManualExecution(order, ticket)));

            Assert.Equal(20236, refusal.Code);
            Assert.Equal(403, refusal.HttpStatus);
            Assert.Single(harness.ManualRefundAuthorizations.ObservedRequests);
        }

        [Fact]
        public async Task A_denied_decision_leaves_no_provider_value_or_domain_trace()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            harness.ManualRefundAuthorizations.Outcome = ManualRefundAuthorizationOutcome.Denied;
            harness.ManualRefundAuthorizations.Detail = "above the actor's settlement ceiling";

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(ManualExecution(order, ticket)));

            Assert.Equal(20236, refusal.Code);

            await AssertNothingHappenedAsync(harness, order, ticket);
        }

        [Fact]
        public async Task An_unavailable_decision_fails_closed()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            harness.ManualRefundAuthorizations.Outcome = ManualRefundAuthorizationOutcome.Unavailable;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(ManualExecution(order, ticket)));

            Assert.Equal(20237, refusal.Code);
            Assert.Equal(502, refusal.HttpStatus);

            await AssertNothingHappenedAsync(harness, order, ticket);
        }

        [Fact]
        public async Task An_unreachable_authority_fails_closed()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            harness.ManualRefundAuthorizations.Outcome = ManualRefundAuthorizationOutcome.Approved;
            harness.ManualRefundAuthorizations.ThrowOnAuthorize = true;

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(ManualExecution(order, ticket)));

            Assert.Equal(20237, refusal.Code);

            await AssertNothingHappenedAsync(harness, order, ticket);
        }

        [Fact]
        public async Task An_affirmative_decision_allows_the_manual_pipeline()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            harness.ManualRefundAuthorizations.Outcome = ManualRefundAuthorizationOutcome.Approved;

            var outcome = await harness.Refund.RefundAsync(ManualExecution(order, ticket));

            var refunded = await TicketAsync(order.Id, ticket.Id);
            var record = Assert.Single(refunded.Refunds);
            var request = Assert.Single(harness.ManualRefundAuthorizations.ObservedRequests);

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(PricingSource.Manual, record.PricingSource);
            Assert.Equal(AuthorityReference, record.ManualAuthority!.Reference);
            Assert.Equal(Reason, record.ManualAuthority.Reason);
            Assert.Equal(ApprovedAmount, record.ApprovedAmount);

            Assert.Equal(order.Id, request.OrderId);
            Assert.Equal(ticket.Id, request.ElectronicTicketId);
            Assert.Equal(outcome.OperationId, request.OperationId);
            Assert.Equal(900, request.ActorId);
            Assert.Equal(BusinessContextType.Airline, request.ContextType);
            Assert.Equal(AuthorizationSurface.Backoffice, request.Surface);
            Assert.Equal(ApprovedAmount, request.ApprovedRefundAmount);
            Assert.Equal(order.CurrencyId, request.CurrencyId);
            Assert.Equal(AuthorityReference, request.AuthorityReference);
            Assert.Equal(Reason, request.Reason);
        }

        [Fact]
        public async Task An_automated_refund_never_consults_the_manual_authorization_authority()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var scope = ticket.RefundableCouponIds().ToList();

            harness.RefundQuotes.Quote(QuoteOf(order, ticket, scope), AcceptedOf(order, ticket, scope));

            var outcome = await harness.Refund.RefundAsync(
                new RefundExecution(
                    order.Id, ticket.Id, scope, NewKey(), order.CommercialVersion, QuotedRefundId: QuoteId));

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(PricingSource.PricingEngine, outcome.PricingSource);
            Assert.Empty(harness.ManualRefundAuthorizations.ObservedRequests);
        }

        private async Task AssertNothingHappenedAsync(
            OrderSliceHarness harness,
            Order order,
            ElectronicTicket ticket)
        {
            var after = await ReloadAsync(order.Id);
            var untouched = await TicketAsync(order.Id, ticket.Id);

            Assert.Empty(harness.DocumentRefunds.ObservedEligibilityKeys);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundKeys);
            Assert.Empty(harness.RefundValues.ObservedRequests);

            Assert.Empty(untouched.Refunds);
            Assert.Equal(ElectronicTicketStatus.Issued, untouched.StatusSummary);
            Assert.Equal(ticket.DocumentVersion, untouched.DocumentVersion);
            Assert.All(untouched.Coupons, coupon =>
                Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));

            Assert.Equal(order.CommercialVersion, after.CommercialVersion);
            Assert.Equal(order.FinancialSequence, after.FinancialSequence);
            Assert.Equal(order.CustomerTotal, after.CustomerTotal);
            Assert.DoesNotContain(after.Changes, change => change.ChangeType == OrderChangeType.Refund);
        }

        private static RefundExecution ManualExecution(Order order, ElectronicTicket ticket)
            => new(
                order.Id,
                ticket.Id,
                ticket.RefundableCouponIds().ToList(),
                NewKey(),
                order.CommercialVersion,
                Manual: new ManualRefundInstruction(
                    AuthorityReference,
                    Reason,
                    ApprovedAmount,
                    Disposition,
                    RefundLines(order),
                    DispositionReference: "FOP-MANUAL-1",
                    SourceRefundType: "Manual"));

        private const string QuoteId = "RFND-AUTOMATED-1";

        private static RefundQuote QuoteOf(Order order, ElectronicTicket ticket, IReadOnlyList<long> scope)
            => new(
                "AirPrice",
                QuoteId,
                PricingSource.PricingEngine,
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                order.CurrencyId,
                scope,
                RefundLines(order),
                ApprovedAmount,
                Disposition,
                DateTimeOffset.UtcNow.AddHours(1));

        private static AcceptedRefund AcceptedOf(Order order, ElectronicTicket ticket, IReadOnlyList<long> scope)
            => new(
                "AirPrice",
                QuoteId,
                PricingSource.PricingEngine,
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                order.CurrencyId,
                scope,
                RefundLines(order),
                ApprovedAmount,
                Disposition,
                DateTimeOffset.UtcNow.AddHours(1));

        private static IReadOnlyList<AcceptedRefundPricingLine> RefundLines(Order order)
            =>
            [
                new(
                    PricingComponentType.Fare,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Credit,
                    PricingLineRole.Adjustment,
                    ApprovedAmount,
                    order.CurrencyId,
                    ApprovedAmount,
                    order.CurrencyId,
                    PricingBasisType.Order,
                    RefundabilityRule.Refundable,
                    Code: "RFND-FARE")
            ];

        private OrderSliceHarness NewHarness()
            => new(_fixture, TestCallerContexts.AirlineUser(7401, $"manual-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private async Task<Order> TicketedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var created = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            return await ReloadAsync(created.Id);
        }

        private async Task<ElectronicTicket> FirstTicketAsync(long orderId)
            => (await TicketsAsync(orderId)).OrderBy(ticket => ticket.Id).First();

        private async Task<ElectronicTicket> TicketAsync(long orderId, long ticketId)
            => (await TicketsAsync(orderId)).Single(ticket => ticket.Id == ticketId);

        private async Task<IReadOnlyList<ElectronicTicket>> TicketsAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await new ElectronicTicketRepository(command).ListByOrderAsync(orderId);
        }

        private async Task<Order> ReloadAsync(long orderId)
        {
            await using var context = _fixture.NewCommandContext();

            return (await new OrderRepository(context).GetAsync(orderId))!;
        }
    }
}
