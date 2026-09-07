using FluentValidation;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.SplitOrder
{
    public sealed class SplitOrderCommandValidator : AbstractValidator<SplitOrderCommand>
    {
        public SplitOrderCommandValidator()
        {
            RuleFor(command => command.OrderId).GreaterThan(0);
            RuleFor(command => command.TravellerIds).NotEmpty();
        }
    }
}
