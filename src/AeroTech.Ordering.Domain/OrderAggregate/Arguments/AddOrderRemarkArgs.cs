using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate.Arguments
{
    public sealed record AddOrderRemarkArgs(
        OrderRemarkType Type,
        OrderRemarkVisibility Visibility,
        OrderRemarkScope Scope,
        string Text,
        long CreatedBy,
        long? TravellerId = null,
        long? SegmentId = null,
        long? OrderItemId = null,
        long? OrderServiceId = null,
        long? DocumentId = null,
        string? CategoryCode = null,
        bool IsPrintedOnItinerary = false,
        bool IsPrintedOnInvoice = false);
}
