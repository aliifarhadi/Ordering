using AeroTech.Messages.Core.Enums;

namespace AeroTech.Messages.Core.IntegrationEvents.V1
{
    public sealed record TravelAgencyOfficeDefined(
        long TravelAgencyOfficeId,
        long TravelAgencyId,
        long? ParentOfficeId,
        string Code,
        string Name,
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
