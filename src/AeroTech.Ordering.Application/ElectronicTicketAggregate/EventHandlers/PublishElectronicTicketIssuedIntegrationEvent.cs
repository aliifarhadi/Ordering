using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicTicketIssued;
using IntegrationCoupon = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicTicketIssuedCoupon;

namespace AeroTech.Ordering.Application.ElectronicTicketAggregate.EventHandlers
{
    public sealed class PublishElectronicTicketIssuedIntegrationEvent : INotificationHandler<DomainEventNotification<ElectronicTicketIssued>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishElectronicTicketIssuedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<ElectronicTicketIssued> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.ElectronicTicketId,
                @event.OrderId,
                @event.TravelerId,
                @event.OperationId,
                @event.DocumentNumber,
                @event.IssuerCarrierId,
                @event.IssuingOfficeId,
                @event.Authority,
                @event.CurrencyId,
                @event.IssuedTotal,
                @event.DocumentVersion,
                @event.PredecessorElectronicTicketId,
                @event.Coupons
                    .Select(coupon => new IntegrationCoupon(
                        coupon.TicketCouponId,
                        coupon.CouponNumber,
                        coupon.OrderServiceId,
                        coupon.JourneySegmentId,
                        coupon.IssuanceValue,
                        coupon.PredecessorTicketCouponId))
                    .ToList()), @event, cancellationToken);
        }
    }
}
