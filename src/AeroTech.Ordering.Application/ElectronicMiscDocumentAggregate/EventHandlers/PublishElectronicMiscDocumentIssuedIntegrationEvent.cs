using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicMiscDocumentIssued;
using IntegrationCoupon = AeroTech.Messages.Ordering.IntegrationEvents.V1.ElectronicMiscDocumentIssuedCoupon;

namespace AeroTech.Ordering.Application.ElectronicMiscDocumentAggregate.EventHandlers
{
    public sealed class PublishElectronicMiscDocumentIssuedIntegrationEvent : INotificationHandler<DomainEventNotification<ElectronicMiscDocumentIssued>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishElectronicMiscDocumentIssuedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<ElectronicMiscDocumentIssued> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.ElectronicMiscDocumentId,
                @event.OrderId,
                @event.TravelerId,
                @event.OperationId,
                @event.DocumentNumber,
                @event.Type,
                @event.ReasonForIssuanceCode,
                @event.IssuerCarrierId,
                @event.IssuingOfficeId,
                @event.Authority,
                @event.CurrencyId,
                @event.IssuedTotal,
                @event.DocumentVersion,
                @event.Coupons
                    .Select(coupon => new IntegrationCoupon(
                        coupon.EmdCouponId,
                        coupon.CouponNumber,
                        coupon.Purpose,
                        coupon.OrderServiceId,
                        coupon.AssociatedTicketCouponId,
                        coupon.IssuanceValue))
                    .ToList()), @event, cancellationToken);
        }
    }
}
