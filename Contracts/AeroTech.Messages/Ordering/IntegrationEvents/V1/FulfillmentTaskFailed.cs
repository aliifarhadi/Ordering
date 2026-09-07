using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record FulfillmentTaskFailed(
        long TaskId,
        long OrderId,
        OrderFulfillmentTaskType TaskType,
        OrderFulfillmentPurpose Purpose,
        string Error) : BaseIntegrationEvent;
}
