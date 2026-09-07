using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.TrafficDocumentReassigned;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate.EventHandlers
{
    public sealed class PublishTrafficDocumentReassignedIntegrationEvent : INotificationHandler<DomainEventNotification<TrafficDocumentReassigned>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishTrafficDocumentReassignedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<TrafficDocumentReassigned> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.DocumentId,
                @event.DocumentNumber,
                @event.SourceOrderId,
                @event.NewOrderId,
                @event.NewTravellerId), @event, cancellationToken);
        }
    }
}
