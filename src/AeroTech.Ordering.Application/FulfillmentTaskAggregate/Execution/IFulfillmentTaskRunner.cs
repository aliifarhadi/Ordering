namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution
{
    public interface IFulfillmentTaskRunner
    {
        Task RunAsync(long fulfillmentTaskId, CancellationToken cancellationToken = default);
    }
}
