using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public void UpdateTimeToLive(DateTimeOffset newTimeToLive, IClock clock)
        {
            if (Status != OrderStatus.Confirmed)
                throw ExceptionFactory.TimeToLiveOnlyUpdatableWhileConfirmed(Id, Status);

            if (newTimeToLive <= clock.GetDateTime())
                throw ExceptionFactory.TimeToLiveMustBeInTheFuture();

            TimeToLive = newTimeToLive;
        }
    }
}
