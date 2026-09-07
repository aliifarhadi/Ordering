using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record AirlineUserOfficeMembershipChanged(
        long AirlineUserId,
        long AirlineOfficeId,
        LifecycleStatus Status,
        long SourceVersion) : BaseIntegrationEvent;
}
