using AeroTech.Messages.Aegis.Enums;

namespace AeroTech.Messages.Aegis.IntegrationEvents.V1
{
    public sealed record PartnerApiAccessChanged(
        long PartnerApiAccessProfileId,
        long TravelAgencyId,
        PartnerApiAccessProfileStatus Status,
        long? DefaultOfficeId,
        IReadOnlyList<long> OfficeScope,
        long SourceVersion,
        DateTimeOffset ChangedAt) : BaseIntegrationEvent;
}
