using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record TravelAgencyStatusChanged(
        long TravelAgencyId,
        AgencyStatus Status,
        long SourceVersion) : BaseIntegrationEvent;
}
