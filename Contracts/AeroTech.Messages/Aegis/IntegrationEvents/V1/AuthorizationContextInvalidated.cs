using AeroTech.Messages.Aegis.Enums;

namespace AeroTech.Messages.Aegis.IntegrationEvents.V1
{
    public sealed record AuthorizationContextInvalidated(
        AuthorizationContextScopeType ContextScopeType,
        long? ScopeId,
        string? ScopeCode,
        string ContextScopeKey,
        long ContextInvalidationVersion,
        AuthorizationContextStatus Status,
        string ReasonCode,
        long SourceVersion,
        DateTimeOffset ChangedAt) : BaseIntegrationEvent;
}
