using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal
{
    public interface IWithdrawOrderService
    {
        Task<WithdrawOrderOutcome> WithdrawAsync(
            long orderId,
            VoidReason reason,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }
}
