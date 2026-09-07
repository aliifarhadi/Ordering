using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.DomainEvents;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.FulfillmentTaskAggregate
{
    public sealed class FulfillmentTask : AggregateRoot<long>
    {
        private readonly List<FulfillmentTaskTarget> _targets = new();
        private readonly List<FulfillmentTaskAttempt> _attempts = new();

        private FulfillmentTask()
        {
        }

        private FulfillmentTask(
            long id,
            long orderId,
            OrderFulfillmentTaskType taskType,
            OrderProviderType providerType,
            string idempotencyKey,
            int maxAttempts,
            int sequence,
            bool canRunInParallel,
            string? supplierCode,
            OrderFulfillmentPurpose purpose,
            VoidReason? cancellationReason)
        {
            Id = id;
            OrderId = orderId;
            TaskType = taskType;
            ProviderType = providerType;
            IdempotencyKey = idempotencyKey;
            MaxAttempts = maxAttempts;
            Sequence = sequence;
            CanRunInParallel = canRunInParallel;
            SupplierCode = supplierCode;
            Purpose = purpose;
            CancellationReason = cancellationReason;
            Status = OrderFulfillmentStatus.Pending;
            AttemptCount = 0;
        }

        public long OrderId { get; private set; }

        public OrderFulfillmentTaskType TaskType { get; private set; }

        public OrderFulfillmentStatus Status { get; private set; }
        public OrderFulfillmentPurpose Purpose { get; private set; }

        public VoidReason? CancellationReason { get; private set; }


        public OrderProviderType ProviderType { get; private set; }

        public string? SupplierCode { get; private set; }

        public int Sequence { get; private set; }

        public bool CanRunInParallel { get; private set; }

        public string IdempotencyKey { get; private set; } = default!;

        public int AttemptCount { get; private set; }

        public int MaxAttempts { get; private set; }

        public DateTimeOffset? NextRetryAt { get; private set; }

        public DateTimeOffset? StartedAt { get; private set; }

        public DateTimeOffset? CompletedAt { get; private set; }

        public DateTimeOffset? ExpiresAt { get; private set; }

        public string? LastError { get; private set; }

        public IReadOnlyCollection<FulfillmentTaskTarget> Targets => _targets.AsReadOnly();

        public IReadOnlyCollection<FulfillmentTaskAttempt> Attempts => _attempts.AsReadOnly();

        public static FulfillmentTask Create(
            long id,
            long orderId,
            OrderFulfillmentTaskType taskType,
            OrderProviderType providerType,
            string idempotencyKey,
            int maxAttempts,
            int sequence,
            bool canRunInParallel = true,
            string? supplierCode = null,
            OrderFulfillmentPurpose purpose = OrderFulfillmentPurpose.InitialReservation,
            VoidReason? cancellationReason = null)
            => new(id, orderId, taskType, providerType, idempotencyKey, maxAttempts, sequence, canRunInParallel, supplierCode, purpose, cancellationReason);

        public FulfillmentTaskTarget AddTarget(
            long id,
            OrderFulfillmentTargetType targetType,
            OrderFulfillmentTargetAction action,
            long? orderServiceId,
            long? orderItemId,
            string? fulfillmentReference = null,
            string? serviceReference = null)
        {
            var target = new FulfillmentTaskTarget(id, Id, targetType, action, orderServiceId, orderItemId, fulfillmentReference, serviceReference);
            _targets.Add(target);
            return target;
        }

        public bool IsDue(DateTimeOffset now)
            => Status == OrderFulfillmentStatus.Pending && (NextRetryAt is null || NextRetryAt <= now);

        public FulfillmentTaskAttempt StartAttempt(long attemptId, long providerInteractionId, bool isRetriable, DateTimeOffset now)
        {
            if (Status != OrderFulfillmentStatus.Pending)
                throw new InvalidOperationException($"Fulfillment task {Id} cannot start an attempt from status {Status}.");

            Status = OrderFulfillmentStatus.InProgress;
            StartedAt = now;
            NextRetryAt = null;
            AttemptCount++;

            var attempt = new FulfillmentTaskAttempt(attemptId, Id, AttemptCount, now, providerInteractionId, isRetriable);
            _attempts.Add(attempt);
            return attempt;
        }

        public void MarkSucceeded(long eventId, DateTimeOffset now, DateTimeOffset? expiresAt = null)
        {
            CurrentAttempt()?.Complete(now);

            Status = OrderFulfillmentStatus.Confirmed;
            CompletedAt = now;
            ExpiresAt = expiresAt;
            LastError = null;

            Causes(new FulfillmentTaskSucceeded(
                eventId.ToString(),
                Id.ToString(),
                now,
                Id,
                OrderId,
                TaskType,
                Purpose));
        }

        public void MarkFailed(long eventId, string error, DateTimeOffset? nextRetryAt, DateTimeOffset now, bool terminal = false)
        {
            CurrentAttempt()?.Fail(now, error);

            LastError = error;

            if (terminal || AttemptCount >= MaxAttempts)
            {
                Status = OrderFulfillmentStatus.Failed;
                NextRetryAt = null;

                Causes(new FulfillmentTaskFailed(
                    eventId.ToString(),
                    Id.ToString(),
                    now,
                    Id,
                    OrderId,
                    TaskType,
                    Purpose,
                    error));

                return;
            }

            Status = OrderFulfillmentStatus.Pending;
            NextRetryAt = nextRetryAt;
        }

        public void RequireManualAction(DateTimeOffset now, string error)
        {
            CurrentAttempt()?.Fail(now, error);

            Status = OrderFulfillmentStatus.ManualActionRequired;
            NextRetryAt = null;
            LastError = error;
        }

        public void MarkCancelled(DateTimeOffset now, string? reason = null)
        {
            if (Status is OrderFulfillmentStatus.Confirmed or OrderFulfillmentStatus.Failed or OrderFulfillmentStatus.Cancelled)
                throw new InvalidOperationException($"Fulfillment task {Id} cannot be cancelled from status {Status}.");

            CurrentAttempt()?.Cancel(now, reason);

            Status = OrderFulfillmentStatus.Cancelled;
            CompletedAt = now;
            NextRetryAt = null;
            LastError = reason;
        }

        private FulfillmentTaskAttempt? CurrentAttempt()
            => _attempts.LastOrDefault(attempt => attempt.Status == OrderFulfillmentStatus.InProgress);

        public void ReopenForRetry()
        {
            if (Status is not (OrderFulfillmentStatus.Failed or OrderFulfillmentStatus.ManualActionRequired))
                throw new InvalidOperationException($"Fulfillment task {Id} cannot be reopened for retry from status {Status}.");

            Status = OrderFulfillmentStatus.Pending;
            NextRetryAt = null;
            LastError = null;
        }

        public void MarkTargetConfirmed(long orderServiceId, string? fulfillmentReference, string? serviceReference)
            => _targets.Single(target => target.OrderServiceId == orderServiceId).MarkConfirmed(fulfillmentReference, serviceReference);

        public IReadOnlyDictionary<long, long> ConfirmedServiceTargets()
            => _targets
                .Where(target => target.OrderServiceId.HasValue && target.Status == OrderFulfillmentStatus.Confirmed)
                .ToDictionary(target => target.OrderServiceId!.Value, target => target.Id);
    }
}
