using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record LegalEntityStatusChanged(
        long LegalEntityId,
        LegalEntityStatus Status,
        DateOnly? ClosedOn,
        long SourceVersion) : BaseIntegrationEvent;
}
