using FluentValidation;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Backoffice
{
    public sealed class BackofficeCreateOrderFromOfferCommandValidator : AbstractValidator<BackofficeCreateOrderFromOfferCommand>
    {
        public BackofficeCreateOrderFromOfferCommandValidator()
        {
            RuleFor(command => command.CustomerId).GreaterThan(0);
            RuleFor(command => command.CreatorUserId).GreaterThan(0);
            RuleFor(command => command.OfferId).NotEmpty();
            RuleFor(command => command.CommissionRate).GreaterThanOrEqualTo(0);
            RuleFor(command => command.Travellers).NotEmpty();
            RuleForEach(command => command.Travellers).ChildRules(traveller =>
                traveller.RuleFor(item => item.FirstName).NotEmpty());
            RuleFor(command => command.Contact).NotNull();
            RuleFor(command => command.Contact.ContactPoints).NotEmpty().When(command => command.Contact is not null);
        }
    }
}
