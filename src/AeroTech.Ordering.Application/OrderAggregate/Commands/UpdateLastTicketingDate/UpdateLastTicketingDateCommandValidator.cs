using FluentValidation;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.UpdateLastTicketingDate
{
    public sealed class UpdateLastTicketingDateCommandValidator : AbstractValidator<UpdateLastTicketingDateCommand>
    {
        public UpdateLastTicketingDateCommandValidator()
        {
            RuleFor(command => command.OrderId).GreaterThan(0);
            RuleFor(command => command.LastTicketingDate).NotEmpty();
        }
    }
}
