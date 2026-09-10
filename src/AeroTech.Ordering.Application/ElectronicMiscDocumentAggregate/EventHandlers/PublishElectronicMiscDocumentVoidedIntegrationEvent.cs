using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicMiscDocumentVoided;

namespace AeroTech.Ordering.Application.ElectronicMiscDocumentAggregate.EventHandlers
{
    public sealed class PublishElectronicMiscDocumentVoidedIntegrationEvent : INotificationHandler<DomainEventNotification<ElectronicMiscDocumentVoided>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishElectronicMiscDocumentVoidedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<ElectronicMiscDocumentVoided> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.ElectronicMiscDocumentId,
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
