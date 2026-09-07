using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public sealed class OrderCancelService : IOrderCancelService
    {
        private const VoidReason CancelReason = VoidReason.CustomerRequest;

        private readonly IOrderRepository _orderRepository;
        private readonly ITrafficDocumentRepository _trafficDocumentRepository;
        private readonly IFulfillmentTaskRepository _taskRepository;
        private readonly IFulfillmentPlanner _planner;
        private readonly IFulfillmentExecutor _executor;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly IDistributedLock _distributedLock;
        private readonly FulfillmentOptions _fulfillmentOptions;

        public OrderCancelService(
            IOrderRepository orderRepository,
            ITrafficDocumentRepository trafficDocumentRepository,
            IFulfillmentTaskRepository taskRepository,
            IFulfillmentPlanner planner,
            IFulfillmentExecutor executor,
            IIdGenerator idGenerator,
            IClock clock,
            IUnitOfWork unitOfWork,
            IOrderQueryDbSynchronizer synchronizer,
            IDistributedLock distributedLock,
            IOptions<FulfillmentOptions> fulfillmentOptions)
        {
            _orderRepository = orderRepository;
            _trafficDocumentRepository = trafficDocumentRepository;
            _taskRepository = taskRepository;
            _planner = planner;
            _executor = executor;
            _idGenerator = idGenerator;
            _clock = clock;
            _unitOfWork = unitOfWork;
            _synchronizer = synchronizer;
            _distributedLock = distributedLock;
            _fulfillmentOptions = fulfillmentOptions.Value;
        }

        public async Task<CancelOrderOutcome> CancelAsync(long orderId, long cancelledBy, CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"order-cancel:{orderId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            if (lockHandle is null)
                return Outcome(order, Array.Empty<long>());

            var idempotencyKey = $"cancel:{orderId}";

            var documents = await _trafficDocumentRepository.GetByOrderAsync(orderId, cancellationToken);

            return documents.Count > 0
                ? await CancelTicketedAsync(order, documents, cancelledBy, idempotencyKey, cancellationToken)
                : await CancelUnticketedAsync(order, cancelledBy, idempotencyKey, cancellationToken);
        }

        private async Task<CancelOrderOutcome> CancelUnticketedAsync(Order order, long cancelledBy, string idempotencyKey, CancellationToken cancellationToken)
        {
            order.EnsureCanBeCancelled();

            var now = _clock.GetDateTime();
            var holdIds = ResolveHoldIds(order);

            if (holdIds.Count == 0)
            {
                order.Cancel(CancelReason, cancelledBy, now, _idGenerator);
                await ProjectAsync(order, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Outcome(order, Array.Empty<long>());
            }

            var (taskIds, allReleased) = await ReleaseAllHoldsAsync(order, holdIds, idempotencyKey, cancellationToken);

            if (allReleased)
                order.Cancel(CancelReason, cancelledBy, now, _idGenerator);
            else
                order.MarkCancelUnconfirmed();

            await ProjectAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Outcome(order, taskIds);
        }

        private async Task<CancelOrderOutcome> CancelTicketedAsync(Order order, IReadOnlyList<TrafficDocument> documents, long cancelledBy, string idempotencyKey, CancellationToken cancellationToken)
        {
            order.EnsureCanBeCancelled();

            var now = _clock.GetDateTime();
            var pending = documents.Where(IsPendingCancel).ToList();

            foreach (var document in pending)
                document.EnsureCanBeCancelled(now);

            var taskIds = new List<long>();
            var allConfirmed = true;

            foreach (var document in pending)
            {
                var task = _planner.PlanVoid(order, document, CancelReason, $"{idempotencyKey}:cancel:{document.Id}");
                await _taskRepository.AddAsync(task, cancellationToken);
                taskIds.Add(task.Id);

                var result = await _executor.ExecuteAttemptAsync(task, order, now, cancellationToken);

                if (!result.Success && result.FailureKind == FulfillmentFailureKind.Permanent)
                    throw ExceptionFactory.TicketCancellationRejectedByProvider(result.Error);

                if (!result.Success)
                {
                    document.MarkCancelUnconfirmed(CancelReason, cancelledBy);
                    allConfirmed = false;
                }
                else
                {
                    document.Cancel(CancelReason, cancelledBy, now);
                }
            }

            if (allConfirmed)
                order.Cancel(CancelReason, cancelledBy, now, _idGenerator);
            else
                order.MarkCancelUnconfirmed();

            await ProjectAsync(order, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Outcome(order, taskIds);
        }

        private async Task<(IReadOnlyList<long> TaskIds, bool AllReleased)> ReleaseAllHoldsAsync(Order order, IReadOnlyList<string> holdIds, string idempotencyKey, CancellationToken cancellationToken)
        {
            var taskIds = new List<long>();
            var allReleased = true;

            foreach (var holdId in holdIds)
            {
                var task = _planner.PlanCancel(order, holdId, $"{idempotencyKey}:release:{holdId}");
                await _taskRepository.AddAsync(task, cancellationToken);
                taskIds.Add(task.Id);

                var result = await _executor.ExecuteAttemptAsync(task, order, _clock.GetDateTime(), cancellationToken);

                if (!result.Success && result.FailureKind == FulfillmentFailureKind.Permanent)
                    throw ExceptionFactory.HoldCouldNotBeReleased(result.Error);

                if (!result.Success)
                    allReleased = false;
            }

            return (taskIds, allReleased);
        }

        private static IReadOnlyList<string> ResolveHoldIds(Order order)
            => order.OrderServices
                .Select(service => service.HoldBatchId)
                .Where(holdBatchId => !string.IsNullOrWhiteSpace(holdBatchId))
                .Select(holdBatchId => holdBatchId!)
                .Distinct()
                .ToList();

        private Task ProjectAsync(Order order, CancellationToken cancellationToken)
            => _synchronizer.ProjectCancelledAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);

        private static bool IsPendingCancel(TrafficDocument document)
            => document.Status is TrafficDocumentStatus.Issued or TrafficDocumentStatus.CancelUnconfirmed;

        private static CancelOrderOutcome Outcome(Order order, IReadOnlyList<long> taskIds)
            => new(order.Id, order.Status, taskIds);
    }
}
