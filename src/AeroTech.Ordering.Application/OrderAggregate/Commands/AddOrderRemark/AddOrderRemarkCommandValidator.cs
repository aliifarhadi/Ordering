using AeroTech.Ordering.Domain.OrderAggregate.Constants;
using AeroTech.Messages.Ordering.Enums;
using FluentValidation;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AddOrderRemark
{
    public sealed class AddOrderRemarkCommandValidator : AbstractValidator<AddOrderRemarkCommand>
    {
        public AddOrderRemarkCommandValidator()
        {
            RuleFor(command => command.OrderId).GreaterThan(0);
            RuleFor(command => command.Text).NotEmpty().MaximumLength(OrderRemarkRules.MaxTextLength);
            RuleFor(command => command.CategoryCode).MaximumLength(OrderRemarkRules.MaxCategoryCodeLength);

            RuleFor(command => command.TravellerId).NotNull().When(command => command.Scope == OrderRemarkScope.Traveller);
            RuleFor(command => command.SegmentId).NotNull().When(command => command.Scope == OrderRemarkScope.Segment);
            RuleFor(command => command.OrderItemId).NotNull().When(command => command.Scope == OrderRemarkScope.OrderItem);
            RuleFor(command => command.OrderServiceId).NotNull().When(command => command.Scope == OrderRemarkScope.OrderService);
            RuleFor(command => command.DocumentId).NotNull().When(command => command.Scope == OrderRemarkScope.Document);
        }
    }
}
