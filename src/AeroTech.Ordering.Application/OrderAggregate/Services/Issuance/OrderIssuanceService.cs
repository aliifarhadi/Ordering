using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.ValueObjects;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Ordering.Application.OrderAggregate.Services.Reservation;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Issuance
{
    public sealed class OrderIssuanceService : IOrderIssuanceService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IFulfillmentTaskRepository _taskRepository;
        private readonly IFulfillmentPlanner _planner;
        private readonly IFulfillmentService _fulfillmentService;
        private readonly ITrafficDocumentRepository _trafficDocumentRepository;
        private readonly ITicketNumberAllocator _ticketNumberAllocator;
        private readonly IOrderQueryDbSynchronizer _synchronizer;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IDistributedLock _distributedLock;
        private readonly IssuanceOptions _issuanceOptions;
        private readonly FulfillmentOptions _fulfillmentOptions;

        public OrderIssuanceService(
            IOrderRepository orderRepository,
            IFulfillmentTaskRepository taskRepository,
            IFulfillmentPlanner planner,
            IFulfillmentService fulfillmentService,
            ITrafficDocumentRepository trafficDocumentRepository,
            ITicketNumberAllocator ticketNumberAllocator,
            IOrderQueryDbSynchronizer synchronizer,
            IIdGenerator idGenerator,
            IClock clock,
            IUnitOfWork unitOfWork,
            IDistributedLock distributedLock,
            IOptions<IssuanceOptions> issuanceOptions,
            IOptions<FulfillmentOptions> fulfillmentOptions)
        {
            _orderRepository = orderRepository;
            _taskRepository = taskRepository;
            _planner = planner;
            _fulfillmentService = fulfillmentService;
            _trafficDocumentRepository = trafficDocumentRepository;
            _ticketNumberAllocator = ticketNumberAllocator;
            _synchronizer = synchronizer;
            _idGenerator = idGenerator;
            _clock = clock;
            _unitOfWork = unitOfWork;
            _distributedLock = distributedLock;
            _issuanceOptions = issuanceOptions.Value;
            _fulfillmentOptions = fulfillmentOptions.Value;
        }

        public async Task<FulfillmentOutcome> IssueAsync(long orderId, CancellationToken cancellationToken = default)
        {
            var (order, taskIds) = await EnsureIssuanceTasksAsync(orderId, cancellationToken);

            var outcomes = await _fulfillmentService.RunTasksAsync(taskIds, cancellationToken);
            var status = await FinalizeIssuanceAsync(order.Id, cancellationToken);

            return new FulfillmentOutcome(order.Id, status, outcomes);
        }

        private async Task<(Order order, IReadOnlyList<long> taskIds)> EnsureIssuanceTasksAsync(
            long orderId,
            CancellationToken cancellationToken)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"order-issue:{orderId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            var idempotencyKey = $"issue:{orderId}";

            var existingTasks = await _taskRepository.GetAllByOrderAndTypeAsync(orderId, OrderFulfillmentTaskType.IssueTicket, cancellationToken);
            var issuedBatchIds = existingTasks
                .SelectMany(task => task.Targets)
                .Select(target => target.FulfillmentReference)
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => reference!)
                .ToHashSet();

            var holdBatchIds = ResolveHoldBatchIds(order);
            if (holdBatchIds.Count == 0)
                throw ExceptionFactory.OrderHasNoReservedHoldsToIssue(orderId);

            var pendingBatchIds = holdBatchIds.Where(batchId => !issuedBatchIds.Contains(batchId)).ToList();

            if (pendingBatchIds.Count > 0 && lockHandle is null)
                return (order, existingTasks.Select(task => task.Id).ToList());

            if (pendingBatchIds.Count > 0 && order.Status != OrderStatus.Ticketing)
                order.RequestIssue();

            var newTaskIds = new List<long>();
            foreach (var holdBatchId in pendingBatchIds)
            {
                var task = _planner.PlanIssue(order, holdBatchId, $"{idempotencyKey}:{holdBatchId}");
                await _taskRepository.AddAsync(task, cancellationToken);
                newTaskIds.Add(task.Id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (order, existingTasks.Select(task => task.Id).Concat(newTaskIds).ToList());
        }

        private static IReadOnlyList<string> ResolveHoldBatchIds(Order order)
            => order.OrderServices
                .Where(service => service.RequiresFulfillment && service.ServiceType == OrderServiceType.AirTransportation)
                .Select(service => service.HoldBatchId)
                .Where(batchId => !string.IsNullOrWhiteSpace(batchId))
                .Select(batchId => batchId!)
                .Distinct()
                .ToList();

        public async Task<OrderStatus> FinalizeIssuanceAsync(long orderId, CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"order-issue:{orderId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            if (lockHandle is null || order.Status != OrderStatus.Ticketing)
                return order.Status;

            var issueTasks = await _taskRepository.GetAllByOrderAndTypeAsync(orderId, OrderFulfillmentTaskType.IssueTicket, cancellationToken);
            if (issueTasks.Count == 0 || issueTasks.Any(task => task.Status != OrderFulfillmentStatus.Confirmed))
                return order.Status;

            var now = _clock.GetDateTime();
            var plan = order.BuildIssuancePlan();
            var issuedServices = new List<IssuedServiceLink>();

            foreach (var travellerPlan in plan.Travellers)
            {
                var ticketNumber = await _ticketNumberAllocator.AllocateAsync(cancellationToken);

                var ticket = TicketDocument.Create(
                    _idGenerator.NewId(),
                    order.Id,
                    travellerPlan.TravellerId,
                    ticketNumber,
                    order.Channel,
                    order.CreatorUserId,
                    order.RecordLocator?.Value,
                    ToDocumentAmounts(travellerPlan.Amounts),
                    now,
                    now.Add(_issuanceOptions.TicketValidity),
                    now.Add(_issuanceOptions.VoidWindow));

                var couponNumber = 1;
                foreach (var couponPlan in travellerPlan.Coupons)
                {
                    var couponId = _idGenerator.NewId();

                    ticket.AddCoupon(couponId, couponPlan.OrderServiceId, couponPlan.OrderSegmentId, couponNumber++, ToDocumentAmounts(couponPlan.Amounts));
                    issuedServices.Add(new IssuedServiceLink(couponPlan.OrderServiceId, ticket.Id, couponId));
                }

                await _trafficDocumentRepository.AddAsync(ticket, cancellationToken);
            }

            order.CompleteIssue(issuedServices, _idGenerator, _clock);

            await _synchronizer.ProjectTicketedAsync(order.ToReadModelSnapshot(now), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return order.Status;
        }

        public async Task<OrderStatus> FailIssuanceAsync(long orderId, string reason, CancellationToken cancellationToken = default)
        {
            await using var lockHandle = await _distributedLock.AcquireAsync(
                $"order-issue:{orderId}",
                TimeSpan.FromSeconds(_fulfillmentOptions.LockExpirySeconds),
                cancellationToken);

            var order = await _orderRepository.GetAsync(orderId, cancellationToken)
                ?? throw ExceptionFactory.OrderNotFound(orderId);

            if (lockHandle is null || order.Status != OrderStatus.Ticketing)
                return order.Status;

            var issueTasks = await _taskRepository.GetAllByOrderAndTypeAsync(orderId, OrderFulfillmentTaskType.IssueTicket, cancellationToken);
            if (!issueTasks.Any(task => task.Status == OrderFulfillmentStatus.Failed))
                return order.Status;

            order.FailIssue(reason, _idGenerator, _clock);

            await _synchronizer.ProjectTicketingFailedAsync(order.ToReadModelSnapshot(_clock.GetDateTime()), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return order.Status;
        }

        private static DocumentAmounts ToDocumentAmounts(TicketAmountBreakdown breakdown)
            => new(breakdown.Fare, breakdown.TaxesTotal, breakdown.FeesTotal, breakdown.Commission, breakdown.TotalAmount, breakdown.CurrencyId);
    }
}
