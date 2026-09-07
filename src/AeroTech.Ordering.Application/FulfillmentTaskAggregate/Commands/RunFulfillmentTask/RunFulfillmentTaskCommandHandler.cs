using MediatR;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Commands.RunFulfillmentTask
{
    public sealed class RunFulfillmentTaskCommandHandler : IRequestHandler<RunFulfillmentTaskCommand, FulfillmentTaskOutcome>
    {
        private readonly IFulfillmentService _fulfillmentService;

        public RunFulfillmentTaskCommandHandler(IFulfillmentService fulfillmentService) => _fulfillmentService = fulfillmentService;

        public Task<FulfillmentTaskOutcome> Handle(RunFulfillmentTaskCommand command, CancellationToken cancellationToken)
            => _fulfillmentService.ExecuteFulfillmentTaskAsync(command.TaskId, cancellationToken);
    }
}
