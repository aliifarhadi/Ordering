using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public sealed class OrderReservationService : IOrderReservationService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IFulfillmentTaskRepository _taskRepository;
        private readonly IFulfillmentPlanner _planner;
        private readonly IFulfillmentService _fulfillmentService;
        private readonly IReservationApplier _reservationApplier;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDistributedLock _distributedLock;
        private readonly FulfillmentOptions _fulfillmentOptions;

        public OrderReservationService(
            IOrderRepository orderRepository,
            IFulfillmentTaskRepository taskRepository,
            IFulfillmentPlanner planner,
            IFulfillmentService fulfillmentService,
            IReservationApplier reservationApplier,
            IUnitOfWork unitOfWork,
            IDistributedLock distributedLock,
            IOptions<FulfillmentOptions> fulfillmentOptions)
        {
            _orderRepository = orderRepository;
            _taskRepository = taskRepository;
            _planner = planner;
            _fulfillmentService = fulfillmentService;
            _reservationApplier = reservationApplier;
            _unitOfWork = unitOfWork;
            _distributedLock = distributedLock;
            _fulfillmentOptions = fulfillmentOptions.Value;
        }

        public async Task<FulfillmentOutcome> ReserveAsync(long orderId, CancellationToken cancellationToken = default)
        {
            var (order, taskIds) = await EnsureReservationTasksAsync(orderId, cancellationToken);

            var outcomes = await _fulfillmentService.RunTasksAsync(taskIds, cancellationToken);
            var status = await FinalizeReservationAsync(order.Id, cancellationToken);

            return new FulfillmentOutcome(order.Id, status, outcomes);
        }

        public async Task<OrderStatus> FinalizeReservationAsync(long orderId, CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"order-reserve:{orderId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            if (lockHandle is null || order.Status is not (OrderStatus.Created or OrderStatus.ReservationUnconfirmed))
                return order.Status;

            var reserveTask = await _taskRepository.GetByOrderAndTypeAsync(orderId, OrderFulfillmentTaskType.ReserveInventory, cancellationToken);
            if (reserveTask is null || reserveTask.Status != OrderFulfillmentStatus.Confirmed)
                return order.Status;

            await _reservationApplier.CompleteAsync(order, reserveTask, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return order.Status;
        }

        public async Task<OrderStatus> FailReservationAsync(long orderId, string reason, CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"order-reserve:{orderId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            if (lockHandle is null || order.Status is not (OrderStatus.Created or OrderStatus.ReservationUnconfirmed))
                return order.Status;

            var reserveTask = await _taskRepository.GetByOrderAndTypeAsync(orderId, OrderFulfillmentTaskType.ReserveInventory, cancellationToken);
            if (reserveTask is null || reserveTask.Status != OrderFulfillmentStatus.Failed)
                return order.Status;

            await _reservationApplier.FailAsync(order, reason, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return order.Status;
        }

        private async Task<(Order order, IReadOnlyList<long> taskIds)> EnsureReservationTasksAsync(
            long orderId,
            CancellationToken cancellationToken)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"order-reserve:{orderId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            var idempotencyKey = $"reserve:{orderId}";

            var existing = await _taskRepository.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (existing is not null)
                return (order, new[] { existing.Id });

            if (lockHandle is null)
                return (order, Array.Empty<long>());

            order.RequestReservation();

            var tasks = _planner.PlanReservation(order, idempotencyKey);
            foreach (var task in tasks)
                await _taskRepository.AddAsync(task, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (order, tasks.Select(task => task.Id).ToList());
        }
    }
}
