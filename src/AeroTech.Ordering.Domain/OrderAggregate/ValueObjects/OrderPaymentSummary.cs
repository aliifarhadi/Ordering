using AeroTech.Framework.Core.Domain.ValueObjects;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.ValueObjects
{
    public sealed class OrderPaymentSummary : ValueObject
    {
        private OrderPaymentSummary()
        {
        }

        public OrderPaymentSummary(
            long paymentId,
            PaymentStatus status,
            decimal? capturedAmount,
            string? providerReference,
            FormOfPayment formOfPayment,
            DateTimeOffset updatedAt)
        {
            PaymentId = paymentId;
            Status = status;
            CapturedAmount = capturedAmount;
            ProviderReference = providerReference;
            FormOfPayment = formOfPayment;
            UpdatedAt = updatedAt;
        }

        public long PaymentId { get; private set; }

        public PaymentStatus Status { get; private set; }

        public decimal? CapturedAmount { get; private set; }

        public string? ProviderReference { get; private set; }

        public FormOfPayment FormOfPayment { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return PaymentId;
            yield return Status;
            yield return CapturedAmount;
            yield return ProviderReference;
            yield return FormOfPayment;
            yield return UpdatedAt;
        }
    }
}
