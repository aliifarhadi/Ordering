using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.EntityFrameworkCore;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal static class ExchangeScenarios
    {
        public const int ClaimConflict = 20070;

        public static async Task<ExchangeScenario> TicketedAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness harness,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            bool roundTrip = false,
            int[]? changedCouponNumbers = null,
            Func<Order, Task>? beforeReservation = null)
        {
            var issued = await IssuedAsync(fixture, harness, roundTrip, beforeReservation);

            return await QuotedAsync(fixture, harness, issued, changedCouponNumbers ?? [1], shapeAccepted);
        }

        public static async Task<IssuedTicket> IssuedAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness harness,
            bool roundTrip = false,
            Func<Order, Task>? beforeReservation = null,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
        {
            await harness.SeedPlatformAsync();

            var created = createOrder is not null
                ? await createOrder(harness)
                : roundTrip
                    ? await harness.CreateOrderAsync()
                    : await harness.CreateOneWayOrderAsync();

            if (beforeReservation is not null)
                await beforeReservation(created);

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            var ticket = (await TicketsAsync(fixture, created.Id)).OrderBy(candidate => candidate.Id).First();

            return new IssuedTicket(created.Id, ticket.Id);
        }

        public static async Task<IssuedTicket> FlownAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness harness,
            int[] flownCouponNumbers,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
        {
            var issued = await IssuedAsync(fixture, harness, roundTrip: true, createOrder: createOrder);
            var ticket = await TicketAsync(fixture, issued.OrderId, issued.TicketId);

            foreach (var couponNumber in flownCouponNumbers)
                await FlyCouponAsync(
                    fixture,
                    issued.TicketId,
                    ticket.Coupons.Single(coupon => coupon.CouponNumber == couponNumber).Id);

            return issued;
        }

        public static async Task<ExchangeScenario> PartiallyUsedAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness setup,
            OrderSliceHarness harness,
            int[] flownCouponNumbers,
            int[] changedCouponNumbers,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
        {
            var issued = await FlownAsync(fixture, setup, flownCouponNumbers, createOrder);

            return await QuotedAsync(fixture, harness, issued, changedCouponNumbers, shapeAccepted);
        }

        public static async Task<ExchangeScenario> QuotedAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness harness,
            IssuedTicket issued,
            int[] changedCouponNumbers,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            string quotedExchangeId = ExchangeSourceFactory.QuoteId)
        {
            var order = await ReloadAsync(fixture, issued.OrderId);
            var ticket = await TicketAsync(fixture, issued.OrderId, issued.TicketId);
            var coupons = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).ToList();
            var couponIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.Id);
            var couponServiceIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.CurrentOrderServiceId);
            var changed = changedCouponNumbers.Select(number => couponServiceIds[number]).Order().ToList();

            Compose(harness, order, changed, quotedExchangeId);

            await harness.Exchange.QuoteAsync(order.Id, changed);

            if (shapeAccepted is not null)
                harness.ExchangeQuotes.Reshape(quotedExchangeId, shapeAccepted);

            return new ExchangeScenario(
                order.Id,
                changed,
                ticket.Id,
                couponIds,
                couponServiceIds,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                ticket.DocumentVersion,
                harness.ExchangeQuotes.Accepted(quotedExchangeId)!);
        }

        public static void Compose(
            OrderSliceHarness harness,
            Order order,
            IReadOnlyList<long> changed,
            string quotedExchangeId = ExchangeSourceFactory.QuoteId)
            => harness.ExchangeQuotes.Composer = request => ExchangeSourceFactory.Compose(
                request,
                ExchangeSourceFactory.ReplacementsFor(order, changed),
                quotedExchangeId: quotedExchangeId);

        public static void ComposeAddCollect(
            OrderSliceHarness harness,
            Order order,
            IReadOnlyList<long> changed,
            decimal addCollectAmount = ExchangeSourceFactory.AddCollectAmount,
            string quotedExchangeId = ExchangeSourceFactory.QuoteId)
            => harness.ExchangeQuotes.Composer = request => ExchangeSourceFactory.Compose(
                request,
                ExchangeSourceFactory.ReplacementsFor(order, changed),
                ChangeMonetaryOutcome.AddCollect,
                quotedExchangeId,
                addCollectAmount);

        public static void ComposeSettlement(
            OrderSliceHarness harness,
            Order order,
            IReadOnlyList<long> changed,
            ChangeMonetaryOutcome monetaryOutcome,
            decimal settlementAmount = ExchangeSourceFactory.NegativeBalanceAmount,
            string quotedExchangeId = ExchangeSourceFactory.QuoteId)
            => harness.ExchangeQuotes.Composer = request => ExchangeSourceFactory.Compose(
                request,
                ExchangeSourceFactory.ReplacementsFor(order, changed),
                monetaryOutcome,
                quotedExchangeId,
                ExchangeSourceFactory.AddCollectAmount,
                settlementAmount);

        public static async Task<ExchangeScenario> NegativeBalanceAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness setup,
            OrderSliceHarness harness,
            ChangeMonetaryOutcome monetaryOutcome,
            int[] changedCouponNumbers,
            int[]? flownCouponNumbers = null,
            decimal settlementAmount = ExchangeSourceFactory.NegativeBalanceAmount,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
        {
            var issued = flownCouponNumbers is { Length: > 0 }
                ? await FlownAsync(fixture, setup, flownCouponNumbers, createOrder)
                : await IssuedAsync(fixture, setup, roundTrip: true, createOrder: createOrder);

            var order = await ReloadAsync(fixture, issued.OrderId);
            var ticket = await TicketAsync(fixture, issued.OrderId, issued.TicketId);
            var coupons = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).ToList();
            var couponIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.Id);
            var couponServiceIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.CurrentOrderServiceId);
            var changed = changedCouponNumbers.Select(number => couponServiceIds[number]).Order().ToList();

            ComposeSettlement(harness, order, changed, monetaryOutcome, settlementAmount);

            await harness.Exchange.QuoteAsync(order.Id, changed);

            if (shapeAccepted is not null)
                harness.ExchangeQuotes.Reshape(ExchangeSourceFactory.QuoteId, shapeAccepted);

            return new ExchangeScenario(
                order.Id,
                changed,
                ticket.Id,
                couponIds,
                couponServiceIds,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                ticket.DocumentVersion,
                harness.ExchangeQuotes.Accepted(ExchangeSourceFactory.QuoteId)!);
        }

        public static async Task<ExchangeScenario> AddCollectAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness setup,
            OrderSliceHarness harness,
            int[] changedCouponNumbers,
            int[]? flownCouponNumbers = null,
            decimal addCollectAmount = ExchangeSourceFactory.AddCollectAmount,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            Func<OrderSliceHarness, Task<Order>>? createOrder = null)
        {
            var issued = flownCouponNumbers is { Length: > 0 }
                ? await FlownAsync(fixture, setup, flownCouponNumbers, createOrder)
                : await IssuedAsync(fixture, setup, roundTrip: true, createOrder: createOrder);

            var order = await ReloadAsync(fixture, issued.OrderId);
            var ticket = await TicketAsync(fixture, issued.OrderId, issued.TicketId);
            var coupons = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).ToList();
            var couponIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.Id);
            var couponServiceIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.CurrentOrderServiceId);
            var changed = changedCouponNumbers.Select(number => couponServiceIds[number]).Order().ToList();

            ComposeAddCollect(harness, order, changed, addCollectAmount);

            await harness.Exchange.QuoteAsync(order.Id, changed);

            if (shapeAccepted is not null)
                harness.ExchangeQuotes.Reshape(ExchangeSourceFactory.QuoteId, shapeAccepted);

            return new ExchangeScenario(
                order.Id,
                changed,
                ticket.Id,
                couponIds,
                couponServiceIds,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                ticket.DocumentVersion,
                harness.ExchangeQuotes.Accepted(ExchangeSourceFactory.QuoteId)!);
        }

        public static async Task FlyCouponAsync(OrderingDatabaseFixture fixture, long ticketId, long ticketCouponId)
        {
            await SetCouponStatusAsync(fixture, ticketCouponId, TicketCouponFinancialStatus.Used);

            await using var command = fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                """
                UPDATE [Order].[ElectronicTickets]
                SET [StatusSummary] = CASE
                        WHEN EXISTS (
                            SELECT 1
                            FROM [Order].[TicketCoupons] AS remaining
                            WHERE remaining.[TicketId] = [Order].[ElectronicTickets].[Id]
                              AND remaining.[FinancialStatus] = {0})
                        THEN {1}
                        ELSE {2}
                    END
                WHERE [Id] = {3}
                """,
                (int)TicketCouponFinancialStatus.Open,
                (int)ElectronicTicketStatus.PartiallyUsed,
                (int)ElectronicTicketStatus.Used,
                ticketId);
        }

        public static async Task SetCouponStatusAsync(
            OrderingDatabaseFixture fixture,
            long ticketCouponId,
            TicketCouponFinancialStatus financialStatus)
        {
            await using var command = fixture.NewCommandContext();

            await command.Database.ExecuteSqlRawAsync(
                "UPDATE [Order].[TicketCoupons] SET [FinancialStatus] = {0} WHERE [Id] = {1}",
                (int)financialStatus, ticketCouponId);
        }

        public static void Register(OrderSliceHarness harness, ExchangeScenario scenario)
            => harness.ExchangeQuotes.Prime(scenario.Accepted);

        public static async Task<int?> SecondOperationCodeAsync(OrderSliceHarness harness, ExchangeScenario scenario)
        {
            try
            {
                await harness.Exchange.ExchangeAsync(scenario.Execution(NewKey()));

                return null;
            }
            catch (BusinessException exception)
            {
                return exception.Code;
            }
        }

        public static string NewKey() => Guid.NewGuid().ToString("N");

        public static async Task<Order> ReloadAsync(OrderingDatabaseFixture fixture, long orderId)
        {
            await using var context = fixture.NewCommandContext();

            return (await new OrderRepository(context).GetAsync(orderId))!;
        }

        public static async Task<IReadOnlyList<ElectronicTicket>> TicketsAsync(OrderingDatabaseFixture fixture, long orderId)
        {
            await using var context = fixture.NewCommandContext();

            return await new ElectronicTicketRepository(context).ListByOrderAsync(orderId);
        }

        public static async Task<ElectronicTicket> TicketAsync(OrderingDatabaseFixture fixture, long orderId, long ticketId)
            => (await TicketsAsync(fixture, orderId)).Single(ticket => ticket.Id == ticketId);

        public static async Task<ElectronicTicket?> FindTicketAsync(OrderingDatabaseFixture fixture, long ticketId)
        {
            await using var context = fixture.NewCommandContext();

            return await new ElectronicTicketRepository(context).GetAsync(ticketId);
        }
    }
}
