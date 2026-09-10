using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents;
using MediatR;
using IntegrationCoupon = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicTicketExchangedCoupon;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicTicketExchanged;

namespace AeroTech.Ordering.Application.ElectronicTicketAggregate.EventHandlers
{
    public sealed class PublishElectronicTicketExchangedIntegrationEvent : INotificationHandler<DomainEventNotification<ElectronicTicketExchanged>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishElectronicTicketExchangedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<ElectronicTicketExchanged> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.ElectronicTicketId,
                @event.OrderId,
                @event.DocumentNumber,
                @event.OperationId,
                @event.ExchangeRecordId,
                @event.SuccessorElectronicTicketId,
                @event.SuccessorDocumentNumber,
                @event.Coupons
                    .Select(coupon => new IntegrationCoupon(
                        coupon.PredecessorTicketCouponId,
                        coupon.PredecessorCouponNumber,
                        coupon.SuccessorTicketCouponId,
                        coupon.SuccessorCouponNumber,
                        coupon.PreviousOrderServiceId,
                        coupon.SuccessorOrderServiceId))
                    .ToList(),
                @event.QuotedExchangeId,
                @event.TargetSelectionRef,
                @event.DocumentVersion), @event, cancellationToken);
        }
    }
}
