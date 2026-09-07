using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution
{
    public sealed class FulfillmentTaskRunner : IFulfillmentTaskRunner
    {
        private readonly IFulfillmentTaskRepository _taskRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IFulfillmentExecutor _executor;
        private readonly IDistributedLock _distributedLock;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;
        private readonly FulfillmentOptions _options;

        public FulfillmentTaskRunner(
            IFulfillmentTaskRepository taskRepository,
            IOrderRepository orderRepository,
            IFulfillmentExecutor executor,
            IDistributedLock distributedLock,
            IIdGenerator idGenerator,
            IClock clock,
            IUnitOfWork unitOfWork,
            IOptions<FulfillmentOptions> options)
        {
            _taskRepository = taskRepository;
            _orderRepository = orderRepository;
            _executor = executor;
            _distributedLock = distributedLock;
            _idGenerator = idGenerator;
            _clock = clock;
            _unitOfWork = unitOfWork;
            _options = options.Value;
        }

        public async Task RunAsync(long fulfillmentTaskId, CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"fulfillment-task:{fulfillmentTaskId}",
                TimeSpan.FromSeconds(_options.LockExpirySeconds),
                cancellationToken);

            if (lockHandle is null)
                return;

            var task = await _taskRepository.GetAsync(fulfillmentTaskId, cancellationToken)
                ?? throw ExceptionFactory.FulfillmentTaskNotFound(fulfillmentTaskId);

            var now = _clock.GetDateTime();
            if (!task.IsDue(now))
                return;

            var order = await _orderRepository.GetAsync(task.OrderId, cancellationToken)
                ?? throw ExceptionFactory.OrderForFulfillmentTaskNotFound(task.OrderId, fulfillmentTaskId);

            var result = await _executor.ExecuteAttemptAsync(task, order, now, cancellationToken);

            if (!result.Success)
            {
                var terminal = result.FailureKind == FulfillmentFailureKind.Permanent;
                task.MarkFailed(_idGenerator.NewId(), result.Error ?? "Fulfillment failed.", ComputeNextRetryAt(task, now), now, terminal);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private DateTimeOffset ComputeNextRetryAt(FulfillmentTask task, DateTimeOffset now)
        {
            var seconds = Math.Min(_options.RetryMaxDelaySeconds, _options.RetryBaseDelaySeconds * Math.Max(1, task.AttemptCount));
            return now.AddSeconds(seconds);
        }
    }
}
