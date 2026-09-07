using FluentValidation;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.CreateOrderFromOffer.Ota
{
    public sealed class OtaCreateOrderFromOfferCommandValidator : AbstractValidator<OtaCreateOrderFromOfferCommand>
    {
        public OtaCreateOrderFromOfferCommandValidator()
        {
            RuleFor(command => command.CustomerId).GreaterThan(0);
            RuleFor(command => command.CreatorUserId).GreaterThan(0);
            RuleFor(command => command.OfferId).NotEmpty();

            RuleFor(command => command.Contact).NotNull();
            When(command => command.Contact is not null, () =>
                RuleFor(command => command.Contact)
                    .Must(contact => contact.EmailAddresses.Count > 0 || contact.Phones.Count > 0)
                    .WithMessage("At least one email address or phone number is required."));

            RuleFor(command => command.Travellers).NotEmpty();
            RuleForEach(command => command.Travellers).ChildRules(traveller =>
            {
                traveller.RuleFor(item => item.Name).NotNull();
                traveller.RuleFor(item => item.Name.FirstName).NotEmpty().When(item => item.Name is not null);
                traveller.RuleFor(item => item.Name.LastName).NotEmpty()
                    .When(item => item.Name is not null && !item.Name.NoLastName);
                traveller.RuleFor(item => item.NationalityId).GreaterThan(0);
                traveller.RuleFor(item => item.CountryOfResidenceId).GreaterThan(0);
            });
        }
    }
}
