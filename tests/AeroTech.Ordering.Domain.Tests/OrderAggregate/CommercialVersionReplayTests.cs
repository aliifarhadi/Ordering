using AeroTech.Framework.Core.Domain.Exceptions;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain.Tests._Shared;
using Xunit;

namespace AeroTech.Ordering.Domain.Tests.OrderAggregate
{
    public sealed class CommercialVersionReplayTests
    {
        private readonly SequentialIdGenerator _ids = new();
        private readonly TestClock _clock = new();

        [Fact]
        public void A_rejected_command_does_not_advance_the_commercial_version()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            Assert.Throws<BusinessException>(() => order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids));

            Assert.Equal(1, order.CommercialVersion);
            Assert.Equal(OrderStatus.Created, order.Status);
        }

        [Fact]
        public void A_no_event_transition_does_not_advance_the_commercial_version()
        {
            var order = Confirmed();

            order.MarkCancelUnconfirmed();

            Assert.Equal(OrderStatus.CancelUnconfirmed, order.Status);
            Assert.Equal(1, order.CommercialVersion);
        }

        [Fact]
        public void A_reservation_retry_sequence_leaves_the_commercial_version_at_one()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);

            order.MarkReservationUnconfirmed(FulfillmentFailureReason.UnknownOutcome, "no response", _ids, _clock);
            order.CompleteReserve("ABC123", _clock.GetDateTime().AddDays(1), Array.Empty<ReservedServiceLink>(), _ids, _clock);

            Assert.Equal(OrderStatus.Confirmed, order.Status);
            Assert.Equal(1, order.CommercialVersion);
        }

        [Fact]
        public void The_emitted_cancellation_event_carries_the_version_the_mutation_produced()
        {
            var order = Confirmed();

            order.Cancel(VoidReason.CustomerRequest, 7, _clock.GetDateTime(), _ids);

            var cancelled = order.GetEvents().OfType<Domain.OrderAggregate.DomainEvents.OrderCancelled>().Single();

            Assert.Equal(order.CommercialVersion, cancelled.CommercialVersion);
            Assert.Equal(2, cancelled.CommercialVersion);
        }

        [Fact]
        public void The_emitted_issue_event_carries_the_unchanged_commercial_version()
        {
            var order = Confirmed();
            order.BeginPayment();
            order.MarkPaid(Summary(order), _ids, _clock);
            order.RequestIssue();
            order.CompleteIssue(Array.Empty<IssuedServiceLink>(), _ids, _clock);

            var issued = order.GetEvents().OfType<Domain.OrderAggregate.DomainEvents.OrderIssued>().Single();

            Assert.Equal(1, issued.CommercialVersion);
        }

        private OrderPaymentSummary Summary(Domain.OrderAggregate.Order order)
            => new(1, PaymentStatus.Captured, order.Amount.GrandTotal, "PSP-1", FormOfPayment.Cash, _clock.GetDateTime());

        private Domain.OrderAggregate.Order Confirmed()
        {
            var order = OrderFactory.CreatedOrder(_ids, _clock);
            order.CompleteReserve("ABC123", _clock.GetDateTime().AddDays(1), Array.Empty<ReservedServiceLink>(), _ids, _clock);
            return order;
        }
    }
}
