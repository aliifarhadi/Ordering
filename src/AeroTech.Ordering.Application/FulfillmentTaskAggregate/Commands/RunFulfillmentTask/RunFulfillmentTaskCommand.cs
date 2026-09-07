using MediatR;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Commands.RunFulfillmentTask
{
    public sealed record RunFulfillmentTaskCommand(long TaskId) : IRequest<FulfillmentTaskOutcome>;
}
