using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderRemark
{
    public sealed record AddOrderRemarkRequest(
        OrderRemarkType Type,
        OrderRemarkVisibility Visibility,
        OrderRemarkScope Scope,
        string Text,
        long? TravellerId,
        long? SegmentId,
        long? OrderItemId,
        long? OrderServiceId,
        long? DocumentId,
        string? CategoryCode,
        bool IsPrintedOnItinerary,
        bool IsPrintedOnInvoice);
}
