using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class PartialRefundFlowTests
    {
        private const string SourceSystem = "AirPrice";
        private const string Disposition = "OriginalFormOfPayment";

        private readonly OrderingDatabaseFixture _fixture;

        public PartialRefundFlowTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task A_used_coupon_is_preserved_while_an_unused_coupon_is_refunded()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var used = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).First();
            var refundable = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).Last();

            await MarkCouponUsedAsync(used.Id);

            await using var harness = NewHarness();
            var current = await ReloadAsync(order.Id);

            Quote(harness, current, ticket, [refundable.Id], 400_000m, 50_000m);

            var outcome = await harness.Refund.RefundAsync(
                Execution(order.Id, ticket, [refundable.Id], QuoteId, NewKey(), current.CommercialVersion));

            var after = await TicketAsync(order.Id, ticket.Id);
            var usedAfter = after.Coupons.Single(coupon => coupon.Id == used.Id);
            var refundedAfter = after.Coupons.Single(coupon => coupon.Id == refundable.Id);

            Assert.Equal(TicketCouponFinancialStatus.Used, usedAfter.FinancialStatus);
            Assert.Equal(TicketCouponFinancialStatus.Refunded, refundedAfter.FinancialStatus);
            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);

            var record = Assert.Single(after.Refunds);

            Assert.Equal([refundable.Id], record.Coupons.Select(coupon => coupon.TicketCouponId).ToList());
            Assert.DoesNotContain(used.Id, record.Coupons.Select(coupon => coupon.TicketCouponId));
            Assert.Equal(ticket.DocumentVersion + 1, after.DocumentVersion);
            Assert.Equal(ElectronicTicketStatus.Refunded, after.StatusSummary);

            var reloaded = await ReloadAsync(order.Id);
            var usedService = reloaded.OrderServices.Single(service => service.Id == used.CurrentOrderServiceId);
            var refundedService = reloaded.OrderServices.Single(service => service.Id == refundable.CurrentOrderServiceId);

            Assert.Equal(OrderServiceDocumentStatus.Issued, usedService.DocumentStatus);
            Assert.Equal(OrderServiceDocumentStatus.Refunded, refundedService.DocumentStatus);
            Assert.Equal([refundable.CurrentOrderServiceId], outcome.RefundedOrderServiceIds.ToList());
        }

        [Fact]
        public async Task A_partial_refund_takes_its_value_only_from_the_accepted_lines()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var used = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).First();
            var refundable = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).Last();

            await MarkCouponUsedAsync(used.Id);

            await using var harness = NewHarness();
            var before = await ReloadAsync(order.Id);
            var issuedTotal = ticket.IssuedTotal;

            Quote(harness, before, ticket, [refundable.Id], 123_456m, 23_456m);

            var outcome = await harness.Refund.RefundAsync(
                Execution(order.Id, ticket, [refundable.Id], QuoteId, NewKey(), before.CommercialVersion));

            var after = await ReloadAsync(order.Id);

            Assert.Equal(100_000m, outcome.ApprovedRefundAmount);
            Assert.Equal(before.CustomerTotal - 100_000m, after.CustomerTotal);
            Assert.NotEqual(issuedTotal, outcome.ApprovedRefundAmount);
            Assert.NotEqual(
                issuedTotal / ticket.Coupons.Count,
                outcome.ApprovedRefundAmount);
        }

        [Fact]
        public async Task A_tax_only_refund_of_a_non_refundable_fare_succeeds()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var scope = ticket.RefundableCouponIds().ToList();

            harness.RefundQuotes.Quote(
                QuoteOf(order, ticket, scope, TaxOnlyLines(order), 60_000m),
                AcceptedOf(order, ticket, scope, TaxOnlyLines(order), 60_000m));

            var before = await ReloadAsync(order.Id);

            var outcome = await harness.Refund.RefundAsync(
                Execution(order.Id, ticket, scope, QuoteId, NewKey(), before.CommercialVersion));

            var after = await ReloadAsync(order.Id);
            var lines = after.PricingLines.Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId).ToList();

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
            Assert.Equal(60_000m, outcome.ApprovedRefundAmount);
            Assert.DoesNotContain(lines, line => line.ComponentType == PricingComponentType.Fare);
            Assert.Contains(lines, line => line.ComponentType == PricingComponentType.Tax);
            Assert.Equal(before.CustomerTotal - 60_000m, after.CustomerTotal);
        }

        [Fact]
        public async Task A_retained_tax_is_not_refunded()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var scope = ticket.RefundableCouponIds().ToList();

            harness.RefundQuotes.Quote(
                QuoteOf(order, ticket, scope, TaxOnlyLines(order), 60_000m),
                AcceptedOf(order, ticket, scope, TaxOnlyLines(order), 60_000m));

            var outcome = await harness.Refund.RefundAsync(
                Execution(order.Id, ticket, scope, QuoteId, NewKey(), order.CommercialVersion));

            var after = await ReloadAsync(order.Id);
            var lines = after.PricingLines.Where(line => line.PriceChangeSetId == outcome.PriceChangeSetId).ToList();

            var refundedTax = Assert.Single(lines, line => line.Code == "RFND-TAX-RETURNABLE");

            Assert.Equal(60_000m, refundedTax.SaleAmount);
            Assert.DoesNotContain(lines, line => line.Code == "RFND-TAX-RETAINED");
            Assert.Equal(60_000m, -lines.Where(line => line.AffectsCustomerBalance).Sum(line => line.SignedSaleAmount));
        }

        [Fact]
        public async Task Successive_partial_refunds_append_without_overwriting_history()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var coupons = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            await using var harness = NewHarness();

            var first = await RefundCouponAsync(harness, order.Id, ticket, coupons[0].Id, "RFND-A", 300_000m);
            var afterFirst = await TicketAsync(order.Id, ticket.Id);

            Assert.Equal(ElectronicTicketStatus.PartiallyUsed, afterFirst.StatusSummary);

            var second = await RefundCouponAsync(harness, order.Id, ticket, coupons[1].Id, "RFND-B", 200_000m);
            var afterSecond = await TicketAsync(order.Id, ticket.Id);

            Assert.Equal(2, afterSecond.Refunds.Count);
            Assert.NotEqual(first.OperationId, second.OperationId);

            var firstRecord = afterSecond.Refunds.Single(record => record.OperationId == first.OperationId);
            var secondRecord = afterSecond.Refunds.Single(record => record.OperationId == second.OperationId);

            Assert.Equal([coupons[0].Id], firstRecord.Coupons.Select(coupon => coupon.TicketCouponId).ToList());
            Assert.Equal([coupons[1].Id], secondRecord.Coupons.Select(coupon => coupon.TicketCouponId).ToList());
            Assert.Equal(300_000m, firstRecord.ApprovedAmount);
            Assert.Equal(200_000m, secondRecord.ApprovedAmount);
            Assert.Equal("RFND-A", firstRecord.QuotedRefundId);
            Assert.Equal(ElectronicTicketStatus.Refunded, afterSecond.StatusSummary);
            Assert.Equal(ticket.DocumentVersion + 2, afterSecond.DocumentVersion);
        }

        [Fact]
        public async Task A_quote_that_prices_a_different_coupon_scope_fails_before_the_provider()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var coupons = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            harness.RefundQuotes.Quote(
                QuoteOf(order, ticket, [coupons[0].Id], RefundLines(order, 300_000m, 0m), 300_000m),
                AcceptedOf(order, ticket, [coupons[1].Id], RefundLines(order, 300_000m, 0m), 300_000m));

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(
                    Execution(order.Id, ticket, [coupons[0].Id], QuoteId, NewKey(), order.CommercialVersion)));

            Assert.Equal(2923, refusal.Code);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundKeys);
            Assert.Empty((await TicketAsync(order.Id, ticket.Id)).Refunds);
        }

        [Fact]
        public async Task The_document_provider_only_receives_the_accepted_coupon_scope()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var coupons = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            await using var harness = NewHarness();

            await RefundCouponAsync(harness, order.Id, ticket, coupons[1].Id, QuoteId, 200_000m);

            var request = Assert.Single(harness.DocumentRefunds.ObservedRefundRequests);

            Assert.Equal([coupons[1].CouponNumber], request.CouponNumbers.ToList());
            Assert.DoesNotContain(coupons[0].CouponNumber, request.CouponNumbers);
        }

        [Fact]
        public async Task A_manual_refund_records_manual_provenance_actor_and_authority()
        {
            await using var harness = NewHarness(TestCallerContexts.AirlineUser(4242, "manual-agent"));
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var scope = ticket.RefundableCouponIds().ToList();

            var outcome = await harness.Refund.RefundAsync(
                new RefundExecution(
                    order.Id,
                    ticket.Id,
                    scope,
                    NewKey(),
                    order.CommercialVersion,
                    Manual: new ManualRefundInstruction(
                        "GOODWILL-2026-114",
                        "Schedule disruption goodwill settlement",
                        500_000m,
                        Disposition,
                        RefundLines(order, 500_000m, 0m),
                        DispositionReference: "FOP-MANUAL-1",
                        SourceRefundType: "Manual")));

            var refunded = await TicketAsync(order.Id, ticket.Id);
            var record = Assert.Single(refunded.Refunds);

            Assert.Equal(PricingSource.Manual, record.PricingSource);
            Assert.True(record.IsManual);
            Assert.Equal("GOODWILL-2026-114", record.ManualAuthority!.Reference);
            Assert.Equal("Schedule disruption goodwill settlement", record.ManualAuthority.Reason);
            Assert.Equal(900L, record.RefundedBy);
            Assert.False(string.IsNullOrWhiteSpace(record.ActorScope));
            Assert.Equal(500_000m, record.ApprovedAmount);
            Assert.Equal("Manual", record.SourceRefundType);

            var after = await ReloadAsync(order.Id);
            var changeSet = after.PriceChangeSets.Single(set => set.Id == outcome.PriceChangeSetId);

            Assert.Equal(PricingSource.Manual, changeSet.Source);
            Assert.Equal(PriceChangeReason.Refund, changeSet.Reason);
        }

        [Fact]
        public async Task A_manual_refund_with_malformed_pricing_fails_before_the_provider()
        {
            await using var harness = NewHarness(TestCallerContexts.AirlineUser(4242, "manual-agent"));
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);
            var scope = ticket.RefundableCouponIds().ToList();

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(
                    new RefundExecution(
                        order.Id,
                        ticket.Id,
                        scope,
                        NewKey(),
                        order.CommercialVersion,
                        Manual: new ManualRefundInstruction(
                            "GOODWILL-2026-115",
                            "Mis-stated settlement",
                            500_000m,
                            Disposition,
                            RefundLines(order, 400_000m, 0m)))));

            Assert.Equal(2935, refusal.Code);
            Assert.Empty(harness.DocumentRefunds.ObservedRefundKeys);
            Assert.Empty((await TicketAsync(order.Id, ticket.Id)).Refunds);
            Assert.Equal(order.CommercialVersion, (await ReloadAsync(order.Id)).CommercialVersion);
        }

        [Fact]
        public async Task A_manual_refund_from_a_non_backoffice_surface_is_refused()
        {
            await using var harness = NewHarness();
            var order = await TicketedOrderAsync(harness);
            var ticket = await FirstTicketAsync(order.Id);

            var refusal = await Assert.ThrowsAsync<BusinessException>(
                () => harness.Refund.RefundAsync(
                    new RefundExecution(
                        order.Id,
                        ticket.Id,
                        ticket.RefundableCouponIds().ToList(),
                        NewKey(),
                        order.CommercialVersion,
                        Manual: new ManualRefundInstruction(
                            "GOODWILL-2026-116",
                            "Agency initiated",
                            500_000m,
                            Disposition,
                            RefundLines(order, 500_000m, 0m)))));

            Assert.Equal(2941, refusal.Code);
            Assert.Equal(403, refusal.HttpStatus);
            Assert.Empty(harness.DocumentRefunds.ObservedEligibilityKeys);
        }

        [Fact]
        public async Task Value_movement_keys_do_not_collide_across_refund_operations()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var coupons = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).ToList();

            await using var harness = NewHarness();

            var first = await RefundCouponAsync(harness, order.Id, ticket, coupons[0].Id, "RFND-A", 300_000m);
            var second = await RefundCouponAsync(harness, order.Id, ticket, coupons[1].Id, "RFND-B", 200_000m);

            var keys = harness.RefundValues.ObservedRequests.Select(request => request.OperationKey).ToList();

            Assert.Equal(2, keys.Count);
            Assert.Equal(keys.Count, keys.Distinct().Count());
            Assert.Contains($"refund-value:{ticket.Id}:{first.OperationId}", keys);
            Assert.Contains($"refund-value:{ticket.Id}:{second.OperationId}", keys);
        }

        [Fact]
        public async Task A_partial_refund_touches_no_inventory_boundary()
        {
            await using var setup = NewHarness();
            var order = await TicketedOrderAsync(setup);
            var ticket = await MultiCouponTicketAsync(order.Id);
            var refundable = ticket.Coupons.OrderBy(coupon => coupon.CouponNumber).Last();

            await using var harness = NewHarness();
            var reservationsBefore = harness.Reservation.ObservedOperationKeys.Count;

            await RefundCouponAsync(harness, order.Id, ticket, refundable.Id, QuoteId, 200_000m);

            var after = await ReloadAsync(order.Id);

            Assert.Equal(reservationsBefore, harness.Reservation.ObservedOperationKeys.Count);
            Assert.Empty(harness.Reservation.ObservedRecoveryKeys);
            Assert.DoesNotContain(after.OrderServices, service => service.Status == OrderServiceStatus.Cancelled);
            Assert.NotEqual(CommercialSummary.Cancelled, after.CommercialSummary);
        }

        private const string QuoteId = "RFND-PARTIAL-1";

        private async Task<RefundOutcome> RefundCouponAsync(
            OrderSliceHarness harness,
            long orderId,
            ElectronicTicket ticket,
            long couponId,
            string quotedRefundId,
            decimal refundedFare)
        {
            var current = await ReloadAsync(orderId);

            harness.RefundQuotes.Quote(
                QuoteOf(current, ticket, [couponId], RefundLines(current, refundedFare, 0m), refundedFare)
                    with { QuotedRefundId = quotedRefundId },
                AcceptedOf(current, ticket, [couponId], RefundLines(current, refundedFare, 0m), refundedFare)
                    with { QuotedRefundId = quotedRefundId });

            return await harness.Refund.RefundAsync(
                Execution(orderId, ticket, [couponId], quotedRefundId, NewKey(), current.CommercialVersion));
        }

        private static void Quote(
            OrderSliceHarness harness,
            Order order,
            ElectronicTicket ticket,
            IReadOnlyList<long> scope,
            decimal refundedFare,
            decimal penalty)
            => harness.RefundQuotes.Quote(
                QuoteOf(order, ticket, scope, RefundLines(order, refundedFare, penalty), refundedFare - penalty),
                AcceptedOf(order, ticket, scope, RefundLines(order, refundedFare, penalty), refundedFare - penalty));

        private static RefundQuote QuoteOf(
            Order order,
            ElectronicTicket ticket,
            IReadOnlyList<long> scope,
            IReadOnlyList<AcceptedRefundPricingLine> lines,
            decimal approvedRefundAmount)
            => new(
                SourceSystem,
                QuoteId,
                PricingSource.PricingEngine,
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                order.CurrencyId,
                scope,
                lines,
                approvedRefundAmount,
                Disposition,
                DateTimeOffset.UtcNow.AddHours(1),
                SourcePricingReference: "AIRPRICE-PARTIAL-1",
                SourceRefundType: "PartialUsed",
                SourceEvidence: "{\"fareUsed\":\"authority-supplied\"}");

        private static AcceptedRefund AcceptedOf(
            Order order,
            ElectronicTicket ticket,
            IReadOnlyList<long> scope,
            IReadOnlyList<AcceptedRefundPricingLine> lines,
            decimal approvedRefundAmount)
            => new(
                SourceSystem,
                QuoteId,
                PricingSource.PricingEngine,
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                order.CurrencyId,
                scope,
                lines,
                approvedRefundAmount,
                Disposition,
                DateTimeOffset.UtcNow.AddHours(1),
                SourcePricingReference: "AIRPRICE-PARTIAL-1",
                DispositionReference: "FOP-1",
                SourceRefundType: "PartialUsed",
                SourceEvidence: "{\"fareUsed\":\"authority-supplied\"}");

        private static IReadOnlyList<AcceptedRefundPricingLine> RefundLines(
            Order order,
            decimal refundedFare,
            decimal penalty)
        {
            var lines = new List<AcceptedRefundPricingLine>
            {
                new(
                    PricingComponentType.Fare,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Credit,
                    PricingLineRole.Adjustment,
                    refundedFare,
                    order.CurrencyId,
                    refundedFare,
                    order.CurrencyId,
                    PricingBasisType.Order,
                    RefundabilityRule.Refundable,
                    Code: "RFND-FARE")
            };

            if (penalty > 0m)
                lines.Add(new AcceptedRefundPricingLine(
                    PricingComponentType.Penalty,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Debit,
                    PricingLineRole.Original,
                    penalty,
                    order.CurrencyId,
                    penalty,
                    order.CurrencyId,
                    PricingBasisType.Order,
                    RefundabilityRule.NonRefundable,
                    Code: "RFND-FEE"));

            return lines;
        }

        private static IReadOnlyList<AcceptedRefundPricingLine> TaxOnlyLines(Order order)
            =>
            [
                new(
                    PricingComponentType.Tax,
                    PricingEffect.CustomerBalance,
                    OrderPricingLineDirection.Credit,
                    PricingLineRole.Adjustment,
                    60_000m,
                    order.CurrencyId,
                    60_000m,
                    order.CurrencyId,
                    PricingBasisType.Order,
                    RefundabilityRule.Refundable,
                    Code: "RFND-TAX-RETURNABLE")
            ];

        private static RefundExecution Execution(
            long orderId,
            ElectronicTicket ticket,
            IReadOnlyList<long> ticketCouponIds,
            string quotedRefundId,
            string idempotencyKey,
            int? expectedCommercialVersion)
            => new(
                orderId,
                ticket.Id,
                ticketCouponIds,
                idempotencyKey,
                expectedCommercialVersion,
                quotedRefundId);

        private OrderSliceHarness NewHarness(Domain._Shared.Contracts.ICallerContext? caller = null)
            => new(_fixture, caller ?? TestCallerContexts.AgencyUser(11, $"subject-{Guid.NewGuid():N}"));

        private static string NewKey() => Guid.NewGuid().ToString("N");

        private async Task<Order> TicketedOrderAsync(OrderSliceHarness harness)
        {
            await harness.SeedPlatformAsync();

            var created = await harness.CreateOrderAsync();

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            return await ReloadAsync(created.Id);
        }

        private async Task MarkCouponUsedAsync(long couponId)
        {
            await using var command = _fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[TicketCoupons] SET [FinancialStatus] = {0} WHERE [Id] = {1}",
                (int)TicketCouponFinancialStatus.Used,
                couponId);
        }

        private async Task<ElectronicTicket> MultiCouponTicketAsync(long orderId)
        {
            var tickets = await TicketsAsync(orderId);
            var ticket = tickets.FirstOrDefault(candidate => candidate.Coupons.Count > 1);

            Assert.NotNull(ticket);

            return ticket!;
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
