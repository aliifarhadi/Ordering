using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Entities
{
    public sealed class FulfillmentTaskAttempt : Entity<long>
    {
        private FulfillmentTaskAttempt()
        {
        }

        internal FulfillmentTaskAttempt(long id, long fulfillmentTaskId, int attemptNumber, DateTimeOffset startedAt, long providerInteractionId, bool isRetriable)
        {
            Id = id;
            FulfillmentTaskId = fulfillmentTaskId;
            AttemptNumber = attemptNumber;
            StartedAt = startedAt;
            Status = OrderFulfillmentStatus.InProgress;
            ProviderInteractionId = providerInteractionId;
            IsRetriable = isRetriable;
        }

 

        public long FulfillmentTaskId { get; private set; }

        public int AttemptNumber { get; private set; }

        public OrderFulfillmentStatus Status { get; private set; }

        public DateTimeOffset StartedAt { get; private set; }

        public DateTimeOffset? CompletedAt { get; private set; }

        public string? Error { get; private set; }
        public long ProviderInteractionId { get; private set; }
        public bool IsRetriable { get; private set; }
        internal void Complete(DateTimeOffset now)
        {
            Status = OrderFulfillmentStatus.Confirmed;
            CompletedAt = now;  
        }

        internal void Fail(DateTimeOffset now, string error)
        {
            Status = OrderFulfillmentStatus.Failed;
            CompletedAt = now;
            Error = error;
        }

        internal void Cancel(DateTimeOffset now, string? reason)
        {
            Status = OrderFulfillmentStatus.Cancelled;
            CompletedAt = now;
            Error = reason;
        }
    }
}
