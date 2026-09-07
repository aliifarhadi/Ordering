using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record TravelAgencyUserOfficeMembershipChanged(
        long TravelAgencyUserId,
        long TravelAgencyOfficeId,
        LifecycleStatus Status,
        long SourceVersion) : BaseIntegrationEvent;
}
