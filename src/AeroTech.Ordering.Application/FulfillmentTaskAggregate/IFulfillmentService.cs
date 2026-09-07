namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate
{
    // Generic fulfillment-task engine — stage-agnostic. Stage orchestration (reserve, issue, …)
    // lives in the stage services, which use this to run their tasks.
    public interface IFulfillmentService
    {
        Task<IReadOnlyList<FulfillmentTaskOutcome>> RunTasksAsync(IReadOnlyList<long> taskIds, CancellationToken cancellationToken = default);

        Task<FulfillmentTaskOutcome> ExecuteFulfillmentTaskAsync(long taskId, CancellationToken cancellationToken = default);

        Task<FulfillmentTaskOutcome> RetryFulfillmentAsync(long taskId, CancellationToken cancellationToken = default);
    }
}
