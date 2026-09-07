using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Payment
{
    public interface IPaymentService
    {
        Task<PaymentOutcome> PayAsync(
            long orderId,
            FormOfPayment formOfPayment,
            string? walletReference,
            CancellationToken cancellationToken = default);
    }
}
