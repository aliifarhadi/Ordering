using FluentValidation;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrder
{
    public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
    {
        public CreateOrderCommandValidator()
        {
            RuleFor(command => command.OfferId).NotEmpty();
            RuleFor(command => command.CustomerId).GreaterThan(0);
            RuleFor(command => command.CreatorUserId).GreaterThan(0);
            RuleFor(command => command.Travellers).NotEmpty();
            RuleForEach(command => command.Travellers).ChildRules(traveller =>
            {
                traveller.RuleFor(item => item.FirstName).NotEmpty();
            });
        }
    }
}
