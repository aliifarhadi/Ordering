using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution
{
    public sealed record FulfillmentExecutionContext(FulfillmentTask Task, Order Order);
}
