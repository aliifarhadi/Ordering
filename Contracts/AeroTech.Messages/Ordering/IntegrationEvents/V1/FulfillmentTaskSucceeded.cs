using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record FulfillmentTaskSucceeded(
        long TaskId,
        long OrderId,
        OrderFulfillmentTaskType TaskType,
        OrderFulfillmentPurpose Purpose) : BaseIntegrationEvent;
}
