using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.ElectronicTicketAggregate;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    internal static class ExchangeScenarios
    {
        public const int ClaimConflict = 2700;

        public static async Task<ExchangeScenario> TicketedAsync(
            OrderingDatabaseFixture fixture,
            OrderSliceHarness harness,
            Func<AcceptedExchange, AcceptedExchange>? shapeAccepted = null,
            bool roundTrip = false,
            int[]? changedCouponNumbers = null,
            Func<Order, Task>? beforeReservation = null)
        {
            await harness.SeedPlatformAsync();

            var created = roundTrip
                ? await harness.CreateOrderAsync()
                : await harness.CreateOneWayOrderAsync();

            if (beforeReservation is not null)
                await beforeReservation(created);

            await harness.Reserve.ReserveAsync(created.Id, NewKey(), null);
            await harness.Issue.IssueAsync(created.Id, NewKey(), null);

            var order = await ReloadAsync(fixture, created.Id);
            var ticket = (await TicketsAsync(fixture, order.Id)).OrderBy(candidate => candidate.Id).First();
            var coupons = ticket.Coupons.OrderBy(candidate => candidate.CouponNumber).ToList();
            var couponIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.Id);
            var couponServiceIds = coupons.ToDictionary(coupon => coupon.CouponNumber, coupon => coupon.CurrentOrderServiceId);
            var changed = (changedCouponNumbers ?? [1]).Select(number => couponServiceIds[number]).Order().ToList();

            Compose(harness, order, changed);

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

        public static void Compose(OrderSliceHarness harness, Order order, IReadOnlyList<long> changed)
            => harness.ExchangeQuotes.Composer = request =>
                ExchangeSourceFactory.Compose(request, ExchangeSourceFactory.ReplacementsFor(order, changed));

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
