using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution
{
    public interface IFulfillmentExecutor
    {
        Task<FulfillmentResult> ExecuteAttemptAsync(
            FulfillmentTask task,
            Order order,
            DateTimeOffset now,
            CancellationToken cancellationToken = default);
    }
}
