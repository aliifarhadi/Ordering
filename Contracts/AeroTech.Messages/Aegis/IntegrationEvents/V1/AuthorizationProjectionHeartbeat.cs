using AeroTech.Messages.Aegis.Enums;

namespace AeroTech.Messages.Aegis.IntegrationEvents.V1
{
    public sealed record AuthorizationProjectionHeartbeat(
        long PolicyVersion,
        long AuthorizationHighWatermark,
        long ContextInvalidationHighWatermark,
        DateTimeOffset LastAuthoritativeProcessingAt,
        SourceFreshnessState CoreEligibilityFreshness,
        SourceFreshnessState IdentityPrincipalFreshness,
        int SecurityEnvelopeVersion,
        bool PublicationHealthy,
        DateTimeOffset EmittedAt) : BaseIntegrationEvent;
}
