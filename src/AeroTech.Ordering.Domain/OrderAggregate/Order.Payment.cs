using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public OrderPaymentSummary? PaymentSummary { get; private set; }

        public void BeginPayment()
        {
            if (Status != OrderStatus.Confirmed)
                throw ExceptionFactory.OrderCannotStartPayment(Id, Status);

            TransitionTo(OrderStatus.Paying);
        }

        public void MarkPaid(OrderPaymentSummary summary, IIdGenerator idGenerator, IClock clock)
        {
            PaymentSummary = summary;

            TransitionTo(OrderStatus.Paid);

            var paidAt = clock.GetDateTime();

            Causes(new OrderPaid(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                paidAt,
                Id,
                Status,
                summary.PaymentId,
                summary.CapturedAmount ?? Amount.GrandTotal,
                CurrencyId,
                summary.FormOfPayment,
                summary.ProviderReference,
                paidAt,
                CustomerId,
                AirlineOfficeId));
        }

        public void MarkPaymentFailed(OrderPaymentSummary summary, FulfillmentFailureReason reason, string detail, IIdGenerator idGenerator, IClock clock)
        {
            PaymentSummary = summary;

            TransitionTo(OrderStatus.PaymentFailed);

            Causes(new OrderPaymentFailed(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                Status,
                summary.PaymentId,
                reason,
                detail));
        }

        public void MarkPaymentUnconfirmed(OrderPaymentSummary summary, FulfillmentFailureReason reason, string detail, IIdGenerator idGenerator, IClock clock)
        {
            PaymentSummary = summary;

            TransitionTo(OrderStatus.PaymentUnconfirmed);

            Causes(new OrderPaymentUnconfirmed(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                Status,
                summary.PaymentId,
                reason,
                detail));
        }
    }
}
