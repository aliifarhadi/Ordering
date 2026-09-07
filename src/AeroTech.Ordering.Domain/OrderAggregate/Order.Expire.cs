using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public void Expire(IIdGenerator idGenerator, IClock clock)
        {
            var now = clock.GetDateTime();
            EnsureCanExpire(now);

            TransitionTo(OrderStatus.Expired);
            TimeToLive = null;
            IncrementCommercialVersion();

            Causes(new OrderExpired(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                now,
                Id,
                Status));
        }

        private void EnsureCanExpire(DateTimeOffset now)
        {
            if (Status is not (OrderStatus.Created or OrderStatus.Confirmed or OrderStatus.ReservationUnconfirmed))
                throw ExceptionFactory.OrderCannotExpire(Id, Status);

            if (TimeToLive is null || now < TimeToLive)
                throw ExceptionFactory.OrderHasNotReachedTimeToLive(Id);
        }
    }
}
