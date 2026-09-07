using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record AirlineUserStatusChanged(
        long AirlineUserId,
        BusinessUserStatus Status,
        long SourceVersion) : BaseIntegrationEvent;
}
