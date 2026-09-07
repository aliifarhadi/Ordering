using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate
{
    public interface IFulfillmentPlanner
    {
        IReadOnlyList<FulfillmentTask> PlanReservation(Order order, string idempotencyKey);

        FulfillmentTask PlanIssue(Order order, string holdBatchId, string idempotencyKey);

        FulfillmentTask PlanVoid(Order order, TrafficDocument document, VoidReason reason, string idempotencyKey);

        FulfillmentTask PlanCancel(Order order, string holdId, string idempotencyKey);
    }
}
