using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.PaymentAggregate.DomainEvents;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.PaymentAggregate
{
    public sealed class Payment : AggregateRoot<long>
    {
        private Payment()
        {
        }

        private Payment(
            long id,
            long orderId,
            decimal amount,
            int currencyId,
            FormOfPayment formOfPayment,
            string? walletReference,
            DateTimeOffset createdAt)
        {
            Id = id;
            OrderId = orderId;
            Amount = amount;
            CurrencyId = currencyId;
            FormOfPayment = formOfPayment;
            WalletReference = walletReference;
            IdempotencyKey = $"pay:{id}";
            Status = PaymentStatus.Pending;
            CreatedAt = createdAt;
        }

        public long OrderId { get; private set; }

        public decimal Amount { get; private set; }

        public int CurrencyId { get; private set; }

        public FormOfPayment FormOfPayment { get; private set; }

        public string? WalletReference { get; private set; }

        public string IdempotencyKey { get; private set; } = default!;

        public PaymentStatus Status { get; private set; }

        public string? ProviderReference { get; private set; }

        public FulfillmentFailureReason? FailureReason { get; private set; }

        public decimal VoidedAmount { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset? CompletedAt { get; private set; }

        public DateTimeOffset? VoidedAt { get; private set; }

        public static Payment Create(
            long id,
            long orderId,
            decimal amount,
            int currencyId,
            FormOfPayment formOfPayment,
            string? walletReference,
            DateTimeOffset createdAt)
            => new(id, orderId, amount, currencyId, formOfPayment, walletReference, createdAt);

        public void Complete(string providerReference, IIdGenerator idGenerator, IClock clock)
        {
            Status = PaymentStatus.Captured;
            ProviderReference = providerReference;
            FailureReason = null;
            CompletedAt = clock.GetDateTime();

            Causes(new PaymentCaptured(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                OrderId,
                Amount,
                CurrencyId,
                providerReference));
        }

        public void Fail(FulfillmentFailureReason reason, string detail, IIdGenerator idGenerator, IClock clock)
        {
            Status = PaymentStatus.Declined;
            FailureReason = reason;
            CompletedAt = clock.GetDateTime();

            Causes(new PaymentDeclined(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                OrderId,
                reason,
                detail));
        }

        public void Void(decimal amount, string providerReference, IIdGenerator idGenerator, IClock clock)
        {
            if (Status != PaymentStatus.Captured)
                throw ExceptionFactory.OnlyCapturedPaymentCanBeVoided();

            if (amount <= 0)
                throw ExceptionFactory.VoidAmountMustBeGreaterThanZero();

            if (VoidedAmount + amount > Amount)
                throw ExceptionFactory.VoidAmountExceedsCapturedAmount();

            VoidedAmount += amount;

            if (VoidedAmount >= Amount)
            {
                Status = PaymentStatus.Voided;
                VoidedAt = clock.GetDateTime();
            }

            Causes(new PaymentVoided(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                OrderId,
                amount,
                VoidedAmount,
                CurrencyId,
                providerReference));
        }

        public void MarkUnconfirmed(FulfillmentFailureReason reason, string detail, IIdGenerator idGenerator, IClock clock)
        {
            Status = PaymentStatus.Unconfirmed;
            FailureReason = reason;

            Causes(new PaymentUnconfirmed(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                clock.GetDateTime(),
                Id,
                OrderId,
                reason,
                detail));
        }
    }
}
