using AeroTech.Ordering.Application.OrderAggregate.Services.VoluntaryChange;
using MediatR;

namespace AeroTech.Ordering.Application.OrderAggregate.Commands.AcceptQuotedChange
{
    public sealed class AcceptQuotedChangeCommandHandler : IRequestHandler<AcceptQuotedChangeCommand, VoluntaryChangeOutcome>
    {
        private readonly IVoluntaryChangeService _voluntaryChangeService;

        public AcceptQuotedChangeCommandHandler(IVoluntaryChangeService voluntaryChangeService) => _voluntaryChangeService = voluntaryChangeService;

        public Task<VoluntaryChangeOutcome> Handle(AcceptQuotedChangeCommand command, CancellationToken cancellationToken)
            => _voluntaryChangeService.ChangeAsync(
                new VoluntaryChangeExecution(
                    command.OrderId,
                    command.OrderServiceId,
                    command.QuotedChangeId,
                    command.IdempotencyKey,
                    command.ExpectedCommercialVersion),
                cancellationToken);
    }
}
