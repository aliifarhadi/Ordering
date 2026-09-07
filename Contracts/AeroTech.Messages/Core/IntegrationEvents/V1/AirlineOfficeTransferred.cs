namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record AirlineOfficeTransferred(
        long AirlineOfficeId,
        long FromLegalEntityId,
        long ToLegalEntityId,
        DateOnly EffectiveDate,
        string? ReasonCode,
        long SourceVersion) : BaseIntegrationEvent;
}
