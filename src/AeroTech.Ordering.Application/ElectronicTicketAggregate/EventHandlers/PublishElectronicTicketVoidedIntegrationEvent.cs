using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicTicketVoided;

namespace AeroTech.Ordering.Application.ElectronicTicketAggregate.EventHandlers
{
    public sealed class PublishElectronicTicketVoidedIntegrationEvent : INotificationHandler<DomainEventNotification<ElectronicTicketVoided>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishElectronicTicketVoidedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<ElectronicTicketVoided> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.ElectronicTicketId,
                @event.OrderId,
                @event.DocumentNumber,
                @event.OperationId,
                @event.Reason,
                @event.ReasonDetail,
                @event.VoidedBy,
                @event.VoidedAt,
                @event.ProviderReference,
                @event.DocumentVersion), @event, cancellationToken);
        }
    }
}
