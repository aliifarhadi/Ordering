using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public void EnsureCanBeCancelled()
        {
            if (Status is not (OrderStatus.Confirmed or OrderStatus.ReservationUnconfirmed or OrderStatus.CancelUnconfirmed))
                throw ExceptionFactory.OrderCannotBeCancelled(Id, Status);
        }

        public void Cancel(
            VoidReason reason,
            long cancelledBy,
            DateTimeOffset at,
            IIdGenerator idGenerator,
            long? operationId = null)
        {
            EnsureCanBeCancelled();

            var wasTicketed = _orderServices.Any(service => service.TrafficDocumentId is not null);

            var changeSet = CancelAllServices(at, cancelledBy, operationId, idGenerator);

            var reversalLines = changeSet is null ? [] : BuildPricingLines(changeSet.Id);

            TransitionTo(OrderStatus.Cancelled);
            TimeToLive = null;
            IncrementCommercialVersion();

            Causes(new OrderCancelled(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                at,
                Id,
                AirlineOfficeId,
                RecordLocator?.Value,
                UniqueIdentifierId,
                CommercialVersion,
                NextEventOrdinal(),
                Status,
                Type,
                Channel,
                CustomerId,
                reason,
                cancelledBy,
                at,
                wasTicketed,
                NetOf(reversalLines),
                CurrencyId,
                reversalLines));

            if (changeSet is not null)
                RaisePricingChanged(
                    _changes.Single(change => change.Id == changeSet.ChangeId),
                    changeSet,
                    idGenerator,
                    at);
        }

        public void MarkCancelUnconfirmed()
        {
            EnsureCanBeCancelled();

            TransitionTo(OrderStatus.CancelUnconfirmed);
        }
    }
}
