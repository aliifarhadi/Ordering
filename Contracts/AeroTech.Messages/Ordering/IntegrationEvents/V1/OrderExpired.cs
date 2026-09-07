using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Messages.Ordering.IntegrationEvents.V1
{
    public record OrderExpired(
        long OrderId,
        OrderStatus Status) : BaseIntegrationEvent;
}
