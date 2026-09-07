using AeroTech.Messages.Ordering.Enums;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderRemark
{
    public sealed record AddOrderRemarkCommand(
        long OrderId,
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
        bool IsPrintedOnInvoice) : IRequest<long>;
}
