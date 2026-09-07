using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.OrderAggregate
{
    public sealed class CommercialVersionTests
    {
        private readonly SequentialIdGenerator _ids = new();
        private readonly TestClock _clock = new();

        [Fact]
        public void Create_establishes_commercial_version_one()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Equal(1, order.CommercialVersion);
        }

        [Fact]
        public void Reservation_outcomes_do_not_advance_the_commercial_version()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            order.CompleteReserve("ABC123", _clock.GetDateTime().AddHours(6), Array.Empty<ReservedServiceLink>(), _ids, _clock);

            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.Equal(1, order.CommercialVersion);
        }

        [Fact]
        public void Payment_outcomes_do_not_advance_the_commercial_version()
        {
            var order = Confirmed();

            order.BeginPayment();
            order.MarkPaid(PaidSummary(order), _ids, _clock);

            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.Equal(1, order.CommercialVersion);
        }

        [Fact]
        public void Document_issue_does_not_advance_the_commercial_version()
        {
            var order = Paid();

            order.RequestIssue();
            order.CompleteIssue(Array.Empty<IssuedServiceLink>(), _ids, _clock);

            Assert.Equal(OrderStatus.Ticketed, order.Status);
            Assert.Equal(1, order.CommercialVersion);
        }

        [Fact]
        public void Cancellation_advances_the_commercial_version_exactly_once()
        {
            var order = Confirmed();

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            Assert.Equal(OrderStatus.Cancelled, order.Status);
            Assert.Equal(2, order.CommercialVersion);
        }

        [Fact]
        public void Expiry_advances_the_commercial_version_exactly_once()
        {
            var order = Confirmed();
            _clock.Advance(TimeSpan.FromDays(2));

            order.Expire(_ids, _clock);

            Assert.Equal(OrderStatus.Expired, order.Status);
            Assert.Equal(2, order.CommercialVersion);
        }

        [Fact]
        public void A_full_reserve_pay_issue_cancel_life_reaches_commercial_version_two()
        {
            var order = Paid();
            order.RequestIssue();
            order.CompleteIssue(Array.Empty<IssuedServiceLink>(), _ids, _clock);

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            Assert.Equal(2, order.CommercialVersion);
        }

        private OrderPaymentSummary PaidSummary(Domain.OrderAggregate.Order order)
            => new(1, PaymentStatus.Captured, order.Amount.GrandTotal, "PSP-1", FormOfPayment.Cash, _clock.GetDateTime());

        private Domain.OrderAggregate.Order Confirmed()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            order.CompleteReserve("ABC123", _clock.GetDateTime().AddDays(1), Array.Empty<ReservedServiceLink>(), _ids, _clock);
            return order;
        }

        private Domain.OrderAggregate.Order Paid()
        {
            var order = Confirmed();
            order.BeginPayment();
            order.MarkPaid(PaidSummary(order), _ids, _clock);
            return order;
        }
    }
}
