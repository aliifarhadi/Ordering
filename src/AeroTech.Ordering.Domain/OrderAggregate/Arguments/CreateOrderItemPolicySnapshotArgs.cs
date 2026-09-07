using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record CreateOrderItemPolicySnapshotArgs(
        DeliveryModel DeliveryModel,
        AccountingGranularity AccountingGranularity,
        AssignmentMode AssignmentMode,
        bool RequiresPassenger,
        bool RequiresSegment,
        bool RequiresSupplierConfirmation,
        bool RequiresDocument,
        bool RequiresFulfillment,
        bool CanBeUnassignedAtPurchase,
        bool CanBeTransferred,
        bool CanBePartiallyConsumed,
        string? RefundRuleRef,
        string? ChangeRuleRef,
        string? CancellationRuleRef,
        string? SupplierPolicyRef,
        DateTimeOffset SnapshotAt,
        string SnapshotVersion);
}
