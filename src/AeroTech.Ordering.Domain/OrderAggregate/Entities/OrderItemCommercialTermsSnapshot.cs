using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderItemCommercialTermsSnapshot : Entity<long>
    {
        private OrderItemCommercialTermsSnapshot()
        {
        }

        public OrderItemCommercialTermsSnapshot(long id, long orderItemId, CreateOrderItemCommercialTermsSnapshotArgs args)
        {
            Id = id;
            OrderItemId = orderItemId;
            IsRefundable = args.IsRefundable;
            IsChangeable = args.IsChangeable;
            IsUpgradable = args.IsUpgradable;
            CheckedBaggage = args.CheckedBaggage;
            CabinBaggage = args.CabinBaggage;
            PolicySource = args.PolicySource;
            SourceRuleReference = args.SourceRuleReference;
            TermsCapturedAt = args.TermsCapturedAt;
        }

        public long OrderItemId { get; private set; }

        public bool IsRefundable { get; private set; }

        public bool IsChangeable { get; private set; }

        public bool IsUpgradable { get; private set; }

        public Baggage? CheckedBaggage { get; private set; }

        public Baggage? CabinBaggage { get; private set; }

        public string PolicySource { get; private set; } = default!;

        public string? SourceRuleReference { get; private set; }

        public DateTimeOffset TermsCapturedAt { get; private set; }

        internal OrderItemCommercialTermsSnapshot CopyTo(long newId, long newOrderItemId)
            => new(newId, newOrderItemId, new CreateOrderItemCommercialTermsSnapshotArgs(
                IsRefundable,
                IsChangeable,
                IsUpgradable,
                PolicySource,
                TermsCapturedAt,
                CheckedBaggage?.Copy(),
                CabinBaggage?.Copy(),
                SourceRuleReference));
    }
}
