using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.Tests._Shared;
using AeroTech.Ordering.Persistence.OrderAggregate;
using AeroTech.Ordering.Persistence.Tests._Shared;
using AeroTech.Ordering.Persistence.Tests.P1;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static AeroTech.Ordering.Persistence.Tests.P3.ExchangeScenarios;

namespace AeroTech.Ordering.Persistence.Tests.P3
{
    [Collection(OrderingDatabaseCollection.Name)]
    public sealed class NoShowAndRemarkNegativeGuaranteeTests
    {
        private const string SsrShapedText = "SSR DOCS HK1 / WCHR / PASSENGER DID NOT TRAVEL / NOSHOW";

        private readonly OrderingDatabaseFixture _fixture;

        public NoShowAndRemarkNegativeGuaranteeTests(OrderingDatabaseFixture fixture) => _fixture = fixture;

        [Fact]
        public async Task R30_An_elapsed_departure_never_creates_a_no_show()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            harness.Clock.Advance(TimeSpan.FromDays(400));

            var order = await ReloadAsync(_fixture, issued.OrderId);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.All(
                order.Segments,
                segment => Assert.True(segment.DepartureDateTime < harness.Clock.GetDateTime()));

            Assert.All(
                order.OrderServices,
                service => Assert.NotEqual(OrderServiceDeliveryStatus.NoShow, service.DeliveryStatus));

            Assert.All(
                ticket.Coupons,
                coupon => Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus));
        }

        [Fact]
        public async Task R31_An_open_coupon_with_no_usage_evidence_never_creates_a_no_show()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            harness.Clock.Advance(TimeSpan.FromDays(400));

            var order = await ReloadAsync(_fixture, issued.OrderId);
            var ticket = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.All(
                ticket.Coupons,
                coupon =>
                {
                    Assert.Equal(TicketCouponControlStatus.Local, coupon.ControlStatus);
                    Assert.Equal(TicketCouponFinancialStatus.Open, coupon.FinancialStatus);
                });

            Assert.All(
                order.OrderServices,
                service => Assert.Contains(
                    service.DeliveryStatus,
                    new[] { OrderServiceDeliveryStatus.NotReady, OrderServiceDeliveryStatus.Unused }));
        }

        [Fact]
        public async Task R32_R33_Free_text_evidence_never_creates_a_no_show_or_changes_eligibility()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            await AddRemarkAsync(harness, issued.OrderId);

            harness.Clock.Advance(TimeSpan.FromDays(400));

            var order = await ReloadAsync(_fixture, issued.OrderId);

            Assert.Equal(1, await RemarkCountAsync(issued.OrderId));

            Assert.All(
                order.OrderServices,
                service => Assert.NotEqual(OrderServiceDeliveryStatus.NoShow, service.DeliveryStatus));

            await using var voiding = NewHarness();

            var outcome = await voiding.VoidDocument.VoidAsync(
                issued.OrderId, issued.TicketId, VoidReason.AgentError, "ssr text is not authority", 7, NewKey());

            Assert.Equal(ServicingOperationStatus.Completed, outcome.OperationStatus);
        }

        [Fact]
        public async Task R34_Free_text_evidence_never_changes_the_coupon_control_status()
        {
            await using var harness = NewHarness();
            var issued = await IssuedAsync(_fixture, harness);

            var before = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            await AddRemarkAsync(harness, issued.OrderId);

            var after = await TicketAsync(_fixture, issued.OrderId, issued.TicketId);

            Assert.Equal(
                before.Coupons.Select(coupon => (coupon.Id, coupon.ControlStatus)),
                after.Coupons.Select(coupon => (coupon.Id, coupon.ControlStatus)));

            Assert.All(
                after.Coupons,
                coupon => Assert.Equal(TicketCouponControlStatus.Local, coupon.ControlStatus));
        }

        private async Task<int> RemarkCountAsync(long orderId)
        {
            await using var command = _fixture.NewCommandContext();

            return await command.Database
                .SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM [Order].[OrderRemarks] WHERE [OrderId] = {0} AND [Text] = {1}",
                    orderId,
                    SsrShapedText)
                .SingleAsync();
        }

        private async Task AddRemarkAsync(OrderSliceHarness harness, long orderId)
        {
            var order = (await harness.Orders.GetAsync(orderId))!;

            order.AddRemark(
                new AddOrderRemarkArgs(
                    OrderRemarkType.General,
                    OrderRemarkVisibility.Internal,
                    OrderRemarkScope.Order,
                    SsrShapedText,
                    7),
                harness.Ids,
                harness.Clock);

            await harness.UnitOfWork.SaveChangesAsync();
        }

        private OrderSliceHarness NewHarness() => new(_fixture, Caller());

        private static ICallerContext Caller()
            => TestCallerContexts.AirlineUser(7438, $"noshow-{Guid.NewGuid():N}");
    }
}
