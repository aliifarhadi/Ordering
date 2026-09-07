using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record IndividualStatusChanged(
        long IndividualId,
        LifecycleStatus Status,
        long SourceVersion) : BaseIntegrationEvent;
}
