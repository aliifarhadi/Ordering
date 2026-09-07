using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record TravelAgencyUserModified(
        long TravelAgencyUserId,
        long TravelAgencyId,
        long? DefaultTravelAgencyOfficeId,
        BusinessUserStatus Status,
        long SourceVersion) : BaseIntegrationEvent;
}
