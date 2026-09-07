using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate
{
    public sealed class TrafficDocumentVoidService : ITrafficDocumentVoidService
    {
        private readonly ITrafficDocumentRepository _trafficDocumentRepository;
        private readonly IOrderRepository _orderRepository;
        private readonly IFulfillmentPlanner _planner;
        private readonly IFulfillmentTaskRepository _taskRepository;
        private readonly IFulfillmentExecutor _executor;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly IDistributedLock _distributedLock;
        private readonly FulfillmentOptions _fulfillmentOptions;

        public TrafficDocumentVoidService(
            ITrafficDocumentRepository trafficDocumentRepository,
            IOrderRepository orderRepository,
            IFulfillmentPlanner planner,
            IFulfillmentTaskRepository taskRepository,
            IFulfillmentExecutor executor,
            IIdGenerator idGenerator,
            IClock clock,
            IUnitOfWork unitOfWork,
            IOrderQueryDbSynchronizer synchronizer,
            IDistributedLock distributedLock,
            IOptions<FulfillmentOptions> fulfillmentOptions)
        {
            _trafficDocumentRepository = trafficDocumentRepository;
            _orderRepository = orderRepository;
            _planner = planner;
            _taskRepository = taskRepository;
            _executor = executor;
            _idGenerator = idGenerator;
            _clock = clock;
            _unitOfWork = unitOfWork;
            _synchronizer = synchronizer;
            _distributedLock = distributedLock;
            _fulfillmentOptions = fulfillmentOptions.Value;
        }

        public async Task<VoidTrafficDocumentOutcome> VoidAsync(
            long orderId,
            long documentId,
            VoidReason reason,
            string? reasonDetail,
            long voidedBy,
            CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"document-void:{documentId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            var document = await _trafficDocumentRepository.GetAsync(documentId, cancellationToken)
                ?? throw ExceptionFactory.TrafficDocumentNotFound();

            if (document.OrderId != orderId)
                throw ExceptionFactory.DocumentDoesNotBelongToOrder();

            if (lockHandle is null)
                return new VoidTrafficDocumentOutcome(document.Id, document.Status, null, 0);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            var now = _clock.GetDateTime();
            document.EnsureCanBeVoided(now);

            var idempotencyKey = $"void:{documentId}";

            var task = _planner.PlanVoid(order, document, reason, idempotencyKey);
            await _taskRepository.AddAsync(task, cancellationToken);

            var result = await _executor.ExecuteAttemptAsync(task, order, now, cancellationToken);

            if (!result.Success && result.FailureKind == FulfillmentFailureKind.Permanent)
                throw ExceptionFactory.TicketVoidRejectedByProvider(result.Error);

            if (!result.Success)
            {
                document.MarkVoidUnconfirmed(reason, reasonDetail, voidedBy);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return new VoidTrafficDocumentOutcome(document.Id, document.Status, result.FailureReason ?? FulfillmentFailureReason.UnknownOutcome, task.Id);
            }

            var voidedServiceIds = document.Coupons.Select(coupon => coupon.OrderServiceId).Distinct().ToList();

            document.Void(now, reason, reasonDetail, voidedBy);
            order.MarkDocumentVoided(document.Id, document.DocumentNumber, voidedServiceIds, reason, voidedBy, now, _idGenerator);

            await _synchronizer.ProjectVoidedAsync(order.ToReadModelSnapshot(now), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new VoidTrafficDocumentOutcome(document.Id, document.Status, null, task.Id);
        }
    }
}
