using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicTicketRevalidated;

namespace AeroTech.Ordering.Application.ElectronicTicketAggregate.EventHandlers
{
    public sealed class PublishElectronicTicketRevalidatedIntegrationEvent : INotificationHandler<DomainEventNotification<ElectronicTicketRevalidated>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishElectronicTicketRevalidatedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<ElectronicTicketRevalidated> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.ElectronicTicketId,
                @event.OrderId,
                @event.DocumentNumber,
                @event.OperationId,
                @event.RevalidationRecordId,
                @event.TicketCouponId,
                @event.CouponNumber,
                @event.PreviousOrderServiceId,
                @event.NewOrderServiceId,
                @event.QuotedChangeId,
                @event.TargetSelectionRef,
                @event.DocumentVersion), @event, cancellationToken);
        }
    }
}
