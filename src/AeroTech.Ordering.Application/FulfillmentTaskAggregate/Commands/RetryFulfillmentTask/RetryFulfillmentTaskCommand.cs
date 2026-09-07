using MediatR;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Commands.RetryFulfillmentTask
{
    public sealed record RetryFulfillmentTaskCommand(long TaskId) : IRequest<FulfillmentTaskOutcome>;
}
