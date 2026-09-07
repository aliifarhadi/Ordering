using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderTimeLimit : Entity<long>
    {
        private OrderTimeLimit()
        {
        }

        internal OrderTimeLimit(long id, long orderId, TimeLimitType type, DateTimeOffset dueAt, string? sourceReference)
        {
            Id = id;
            OrderId = orderId;
            Type = type;
            DueAt = dueAt;
            SourceReference = sourceReference;
            Status = TimeLimitStatus.Active;
        }

        public long OrderId { get; private set; }

        public TimeLimitType Type { get; private set; }

        public DateTimeOffset DueAt { get; private set; }

        public TimeLimitStatus Status { get; private set; }

        public string? SourceReference { get; private set; }

        public DateTimeOffset? SettledAt { get; private set; }

        internal void Meet(DateTimeOffset at)
        {
            Status = TimeLimitStatus.Met;
            SettledAt = at;
        }

        internal void Cancel(DateTimeOffset at)
        {
            Status = TimeLimitStatus.Cancelled;
            SettledAt = at;
        }

        public bool HasLapsed(DateTimeOffset now) => Status == TimeLimitStatus.Active && now >= DueAt;
    }
}
