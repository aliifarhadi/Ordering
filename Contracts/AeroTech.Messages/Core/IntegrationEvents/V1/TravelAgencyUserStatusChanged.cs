using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record TravelAgencyUserStatusChanged(
        long TravelAgencyUserId,
        BusinessUserStatus Status,
        long SourceVersion) : BaseIntegrationEvent;
}
