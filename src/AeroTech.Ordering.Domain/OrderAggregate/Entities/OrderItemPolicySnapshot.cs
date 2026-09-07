using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Entities
{
    public sealed class OrderItemPolicySnapshot : Entity<long>
    {
        private OrderItemPolicySnapshot()
        {
        }

        public OrderItemPolicySnapshot(long id, long orderItemId, CreateOrderItemPolicySnapshotArgs args)
        {
            Id = id;
            OrderItemId = orderItemId;
            DeliveryModel = args.DeliveryModel;
            AccountingGranularity = args.AccountingGranularity;
            AssignmentMode = args.AssignmentMode;
            RequiresPassenger = args.RequiresPassenger;
            RequiresSegment = args.RequiresSegment;
            RequiresSupplierConfirmation = args.RequiresSupplierConfirmation;
            RequiresDocument = args.RequiresDocument;
            RequiresFulfillment = args.RequiresFulfillment;
            CanBeUnassignedAtPurchase = args.CanBeUnassignedAtPurchase;
            CanBeTransferred = args.CanBeTransferred;
            CanBePartiallyConsumed = args.CanBePartiallyConsumed;
            RefundRuleRef = args.RefundRuleRef;
            ChangeRuleRef = args.ChangeRuleRef;
            CancellationRuleRef = args.CancellationRuleRef;
            SupplierPolicyRef = args.SupplierPolicyRef;
            SnapshotAt = args.SnapshotAt;
            SnapshotVersion = args.SnapshotVersion;
        }

        public long OrderItemId { get; private set; }

        public DeliveryModel DeliveryModel { get; private set; }

        public AccountingGranularity AccountingGranularity { get; private set; }

        public AssignmentMode AssignmentMode { get; private set; }

        public bool RequiresPassenger { get; private set; }

        public bool RequiresSegment { get; private set; }

        public bool RequiresSupplierConfirmation { get; private set; }

        public bool RequiresDocument { get; private set; }

        public bool RequiresFulfillment { get; private set; }

        public bool CanBeUnassignedAtPurchase { get; private set; }

        public bool CanBeTransferred { get; private set; }

        public bool CanBePartiallyConsumed { get; private set; }

        public string? RefundRuleRef { get; private set; }

        public string? ChangeRuleRef { get; private set; }

        public string? CancellationRuleRef { get; private set; }

        public string? SupplierPolicyRef { get; private set; }

        public DateTimeOffset SnapshotAt { get; private set; }

        public string SnapshotVersion { get; private set; } = default!;

        internal OrderItemPolicySnapshot CopyTo(long newId, long newOrderItemId)
            => new(newId, newOrderItemId, new CreateOrderItemPolicySnapshotArgs(
                DeliveryModel, AccountingGranularity, AssignmentMode, RequiresPassenger, RequiresSegment,
                RequiresSupplierConfirmation, RequiresDocument, RequiresFulfillment, CanBeUnassignedAtPurchase,
                CanBeTransferred, CanBePartiallyConsumed, RefundRuleRef, ChangeRuleRef, CancellationRuleRef,
                SupplierPolicyRef, SnapshotAt, SnapshotVersion));
    }
}
