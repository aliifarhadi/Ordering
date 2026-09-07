using MediatR;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Commands.RetryFulfillmentTask
{
    public sealed class RetryFulfillmentTaskCommandHandler : IRequestHandler<RetryFulfillmentTaskCommand, FulfillmentTaskOutcome>
    {
        private readonly IFulfillmentService _fulfillmentService;

        public RetryFulfillmentTaskCommandHandler(IFulfillmentService fulfillmentService) => _fulfillmentService = fulfillmentService;

        public Task<FulfillmentTaskOutcome> Handle(RetryFulfillmentTaskCommand command, CancellationToken cancellationToken)
            => _fulfillmentService.RetryFulfillmentAsync(command.TaskId, cancellationToken);
    }
}
