using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record AirlineOfficeModified(
        long AirlineOfficeId,
        long? ParentOfficeId,
        string Code,
        string Name,
        long? OrganisationUnitId,
        long LegalEntityId,
        DateOnly LegalEntityEffectiveFrom,
        int CountryId,
        int? CityId,
        int? AirportId,
        int? PointOfSaleCountryId,
        int? PointOfSaleCityId,
        string? TimeZoneId,
        OfficeStatus Status,
        DateOnly? ValidFrom,
        DateOnly? ValidTo,
        long SourceVersion) : BaseIntegrationEvent;
}
