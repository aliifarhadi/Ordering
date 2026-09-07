using AeroTech.Messages.Aegis.Enums;
using AeroTech.Messages.Shared.Enums;

namespace AeroTech.Messages.Aegis.IntegrationEvents.V1
{
    public sealed record EffectiveAuthorizationSetChanged(
        long EffectiveAuthorizationSetId,
        GranteeType GranteeType,
        long? AirlineUserId,
        long? TravelAgencyUserId,
        long? IndividualId,
        string? SubjectId,
        long? PartnerApiAccessProfileId,
        string? ServiceCode,
        BusinessContextType ContextType,
        string ContextKey,
        AuthorizationSurface AuthorizationSurface,
        long AuthorizationVersion,
        EffectiveAuthorizationStatus EffectiveStatus,
        IReadOnlyList<string> RoleCodes,
        long? ResolvedCustomerId,
        EligibilityReasonCode? ReasonCode,
        long SourceVersion,
        DateTimeOffset ChangedAt) : BaseIntegrationEvent;
}
