using AeroTech.Ordering.Domain._Shared.Resources;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public sealed class InlineReservationService : IInlineReservationService
    {
        private readonly IFulfillmentPlanner _planner;
        private readonly IFulfillmentTaskRepository _taskRepository;
        private readonly IFulfillmentExecutor _executor;
        private readonly IReservationApplier _reservationApplier;
        private readonly IOrderQueryDbSynchronizer _synchronizer;

        public InlineReservationService(
            IFulfillmentPlanner planner,
            IFulfillmentTaskRepository taskRepository,
            IFulfillmentExecutor executor,
            IReservationApplier reservationApplier,
            IOrderQueryDbSynchronizer synchronizer)
        {
            _planner = planner;
            _taskRepository = taskRepository;
            _executor = executor;
            _reservationApplier = reservationApplier;
            _synchronizer = synchronizer;
        }

        public async Task<InlineReservationOutcome> ReserveAsync(
            Order order,
            string idempotencyKey,
            DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            order.RequestReservation();

            var tasks = _planner.PlanReservation(order, idempotencyKey);
            if (tasks.Count == 0)
            {
                await _synchronizer.ProjectCreatedAsync(order.ToReadModelSnapshot(now), cancellationToken);
                return new InlineReservationOutcome(order.Status, null, null, null);
            }

            foreach (var task in tasks)
                await _taskRepository.AddAsync(task, cancellationToken);

            var reserveTask = tasks[0];
            var result = await _executor.ExecuteAttemptAsync(reserveTask, order, now, cancellationToken);

            if (!result.Success && result.FailureKind == FulfillmentFailureKind.Permanent)
                throw ExceptionFactory.ReservationRejectedByProvider(result.Error);

            if (result.Success)
            {
                var recordLocator = await _reservationApplier.CompleteAsync(order, reserveTask, cancellationToken);
                return new InlineReservationOutcome(order.Status, recordLocator, reserveTask.Id, null);
            }

            var reason = result.FailureReason ?? FulfillmentFailureReason.UnknownOutcome;
            var detail = result.Error ?? "The reservation could not be confirmed.";
            await _reservationApplier.MarkUnconfirmedAsync(order, reserveTask, reason, detail, cancellationToken);
            return new InlineReservationOutcome(order.Status, null, reserveTask.Id, reason);
        }
    }
}
