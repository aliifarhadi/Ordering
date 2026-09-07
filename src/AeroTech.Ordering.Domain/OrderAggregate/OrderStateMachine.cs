using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public static class OrderStateMachine
    {
        private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Transitions =
            new Dictionary<OrderStatus, OrderStatus[]>
            {
                [OrderStatus.None] = new[] { OrderStatus.Created },
                [OrderStatus.Created] = new[] { OrderStatus.Confirmed, OrderStatus.ReservationUnconfirmed, OrderStatus.ReserveFailed, OrderStatus.Cancelled, OrderStatus.Expired },
                [OrderStatus.ReservationUnconfirmed] = new[] { OrderStatus.Confirmed, OrderStatus.ReserveFailed, OrderStatus.Cancelled, OrderStatus.CancelUnconfirmed, OrderStatus.Expired },
                [OrderStatus.Confirmed] = new[] { OrderStatus.Paying, OrderStatus.Cancelled, OrderStatus.CancelUnconfirmed, OrderStatus.Expired },
                [OrderStatus.CancelUnconfirmed] = new[] { OrderStatus.Cancelled, OrderStatus.CancelUnconfirmed, OrderStatus.Expired },
                [OrderStatus.Paying] = new[] { OrderStatus.Paid, OrderStatus.PaymentFailed, OrderStatus.PaymentUnconfirmed },
                [OrderStatus.PaymentUnconfirmed] = new[] { OrderStatus.Paid, OrderStatus.PaymentFailed, OrderStatus.Cancelled, OrderStatus.Expired },
                [OrderStatus.Paid] = new[] { OrderStatus.Ticketing, OrderStatus.Cancelled, OrderStatus.Expired },
                [OrderStatus.Ticketing] = new[] { OrderStatus.Ticketed, OrderStatus.TicketingFailed, OrderStatus.TicketingUnconfirmed },
                [OrderStatus.TicketingUnconfirmed] = new[] { OrderStatus.Ticketed, OrderStatus.TicketingFailed, OrderStatus.Cancelled, OrderStatus.Expired },
                [OrderStatus.Ticketed] = new[] { OrderStatus.Refunded, OrderStatus.Cancelled, OrderStatus.CancelUnconfirmed },
                [OrderStatus.ReserveFailed] = Array.Empty<OrderStatus>(),
                [OrderStatus.PaymentFailed] = Array.Empty<OrderStatus>(),
                [OrderStatus.TicketingFailed] = Array.Empty<OrderStatus>(),
                [OrderStatus.Expired] = Array.Empty<OrderStatus>(),
                [OrderStatus.Cancelled] = Array.Empty<OrderStatus>(),
                [OrderStatus.Refunded] = Array.Empty<OrderStatus>()
            };

        public static bool CanTransition(OrderStatus from, OrderStatus to)
            => Transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

        public static void EnsureCanTransition(OrderStatus from, OrderStatus to)
        {
            if (!CanTransition(from, to))
                throw ExceptionFactory.OrderCannotTransition(from, to);
        }
    }
}
