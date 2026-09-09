namespace AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund
{
    public interface ICancelRefundService
    {
        Task<CancelRefundOutcome> CancelRefundAsync(
            CancelRefundExecution execution,
            CancellationToken cancellationToken = default);
    }
}
