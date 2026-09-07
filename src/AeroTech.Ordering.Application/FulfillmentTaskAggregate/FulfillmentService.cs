using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate
{
    public sealed class FulfillmentService : IFulfillmentService
    {
        private readonly IFulfillmentTaskRepository _taskRepository;
        private readonly IFulfillmentTaskRunner _runner;
        private readonly IUnitOfWork _unitOfWork;

        public FulfillmentService(
            IFulfillmentTaskRepository taskRepository,
            IFulfillmentTaskRunner runner,
            IUnitOfWork unitOfWork)
        {
            _taskRepository = taskRepository;
            _runner = runner;
            _unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyList<FulfillmentTaskOutcome>> RunTasksAsync(IReadOnlyList<long> taskIds, CancellationToken cancellationToken = default)
        {
            var outcomes = new List<FulfillmentTaskOutcome>(taskIds.Count);

            foreach (var taskId in taskIds)
            {
                await _runner.RunAsync(taskId, cancellationToken);

                var task = await _taskRepository.GetAsync(taskId, cancellationToken);
                if (task is not null)
                    outcomes.Add(new FulfillmentTaskOutcome(task.Id, task.Status, task.LastError));
            }

            return outcomes;
        }

        public async Task<FulfillmentTaskOutcome> ExecuteFulfillmentTaskAsync(long taskId, CancellationToken cancellationToken = default)
        {
            await _runner.RunAsync(taskId, cancellationToken);
            return await ReadTaskOutcomeAsync(taskId, cancellationToken);
        }

        public async Task<FulfillmentTaskOutcome> RetryFulfillmentAsync(long taskId, CancellationToken cancellationToken = default)
        {
            var task = await _taskRepository.GetAsync(taskId, cancellationToken)
                ?? throw ExceptionFactory.FulfillmentTaskNotFound(taskId);

            task.ReopenForRetry();
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _runner.RunAsync(taskId, cancellationToken);
            return await ReadTaskOutcomeAsync(taskId, cancellationToken);
        }

        private async Task<FulfillmentTaskOutcome> ReadTaskOutcomeAsync(long taskId, CancellationToken cancellationToken)
        {
            var task = await _taskRepository.GetAsync(taskId, cancellationToken)
                ?? throw ExceptionFactory.FulfillmentTaskNotFound(taskId);

            return new FulfillmentTaskOutcome(task.Id, task.Status, task.LastError);
        }
    }
}
