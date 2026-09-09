using FluentValidation;

namespace AeroTech.Ordering.Application.TrafficDocumentAggregate.Commands.VoidTrafficDocument
{
    public sealed class VoidTrafficDocumentCommandValidator : AbstractValidator<VoidTrafficDocumentCommand>
    {
        public VoidTrafficDocumentCommandValidator()
        {
            RuleFor(command => command.OrderId).GreaterThan(0);
            RuleFor(command => command.DocumentId).GreaterThan(0);
            RuleFor(command => command.IdempotencyKey).NotEmpty();
        }
    }
}
