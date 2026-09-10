using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicTicketRefunded;

namespace AeroTech.Ordering.Application.ElectronicTicketAggregate.EventHandlers
{
    public sealed class PublishElectronicTicketRefundedIntegrationEvent : INotificationHandler<DomainEventNotification<ElectronicTicketRefunded>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishElectronicTicketRefundedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<ElectronicTicketRefunded> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.ElectronicTicketId,
                @event.OrderId,
                @event.DocumentNumber,
                @event.OperationId,
                @event.RefundRecordId,
                @event.QuotedRefundId,
                @event.PricingSource,
                @event.ApprovedAmount,
                @event.CurrencyId,
                @event.ApprovedDisposition,
                @event.TicketCouponIds,
                @event.StatusSummary,
                @event.DocumentVersion), @event, cancellationToken);
        }
    }
}
