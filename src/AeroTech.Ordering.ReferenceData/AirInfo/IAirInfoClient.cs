using AeroTech.Ordering.ReferenceData.AirInfo.Wire;

namespace AeroTech.Ordering.ReferenceData.AirInfo
{
    public interface IAirInfoClient
    {
        Task<List<CurrencyDto>> GetCurrenciesAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default);

        Task<List<AirlineDto>> GetAirlinesAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default);

        Task<List<CityDto>> GetCitiesAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default);

        Task<List<AirportDto>> GetAirportsAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default);
    }
}
