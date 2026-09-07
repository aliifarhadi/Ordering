using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record LegalEntityDefined(
        long LegalEntityId,
        string Code,
        string LegalName,
        string? TradingName,
        LegalEntityKind Kind,
        int CountryOfIncorporationId,
        string? RegistrationNumber,
        LegalEntityStatus Status,
        int? FunctionalCurrencyId,
        int? FiscalYearEndMonth,
        DateOnly? ActiveFrom,
        DateOnly? ClosedOn,
        long SourceVersion) : BaseIntegrationEvent;
}
