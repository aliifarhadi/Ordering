using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.Ordering.Enums;

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
            RefundabilitySummary = args.RefundabilitySummary;
            ChangeabilitySummary = args.ChangeabilitySummary;
            UpgradeEligibilitySummary = args.UpgradeEligibilitySummary;
            SourceSystem = args.SourceSystem;
            SourcePolicyReference = args.SourcePolicyReference;
            SourcePolicyVersion = args.SourcePolicyVersion;
            TermsCapturedAt = args.TermsCapturedAt;
        }

        public long OrderItemId { get; private set; }

        public CommercialTermState RefundabilitySummary { get; private set; }

        public CommercialTermState ChangeabilitySummary { get; private set; }

        public CommercialTermState UpgradeEligibilitySummary { get; private set; }

        public string SourceSystem { get; private set; } = default!;

        public string? SourcePolicyReference { get; private set; }

        public string? SourcePolicyVersion { get; private set; }

        public DateTimeOffset TermsCapturedAt { get; private set; }

        internal OrderItemCommercialTermsSnapshot CopyTo(long newId, long newOrderItemId)
            => new(newId, newOrderItemId, new CreateOrderItemCommercialTermsSnapshotArgs(
                RefundabilitySummary,
                ChangeabilitySummary,
                UpgradeEligibilitySummary,
                SourceSystem,
                TermsCapturedAt,
                SourcePolicyReference,
                SourcePolicyVersion));
    }
}
