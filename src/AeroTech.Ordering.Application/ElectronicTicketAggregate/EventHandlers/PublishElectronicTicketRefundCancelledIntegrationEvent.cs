using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicTicketRefundCancelled;

namespace AeroTech.Ordering.Application.ElectronicTicketAggregate.EventHandlers
{
    public sealed class PublishElectronicTicketRefundCancelledIntegrationEvent : INotificationHandler<DomainEventNotification<ElectronicTicketRefundCancelled>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishElectronicTicketRefundCancelledIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<ElectronicTicketRefundCancelled> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.ElectronicTicketId,
                @event.OrderId,
                @event.DocumentNumber,
                @event.OperationId,
                @event.RefundRecordId,
                @event.CorrectionRecordId,
                @event.OriginalRefundOperationId,
                @event.CorrectedAmount,
                @event.CurrencyId,
                @event.Reason,
                @event.StatusSummary,
                @event.DocumentVersion), @event, cancellationToken);
        }
    }
}
