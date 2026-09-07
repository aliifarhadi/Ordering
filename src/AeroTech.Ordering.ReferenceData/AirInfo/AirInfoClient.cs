using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AeroTech.Ordering.ReferenceData.AirInfo.Wire;

namespace AeroTech.Ordering.ReferenceData.AirInfo
{
    public sealed class AirInfoClient : IAirInfoClient
    {
        private const string CurrenciesRoute = "v1/Financials/Currencies";
        private const string AirlinesRoute = "v1/Locations/Airlines";
        private const string CitiesRoute = "v1/Locations/Cities";
        private const string AirportsRoute = "v1/Locations/Airports";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _httpClient;

        public AirInfoClient(HttpClient httpClient) => _httpClient = httpClient;

        public Task<List<CurrencyDto>> GetCurrenciesAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default)
            => GetListAsync<CurrencyDto>(CurrenciesRoute, modifiedAfter, cancellationToken);

        public Task<List<AirlineDto>> GetAirlinesAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default)
            => GetListAsync<AirlineDto>(AirlinesRoute, modifiedAfter, cancellationToken);

        public Task<List<CityDto>> GetCitiesAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default)
            => GetListAsync<CityDto>(CitiesRoute, modifiedAfter, cancellationToken);

        public Task<List<AirportDto>> GetAirportsAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default)
            => GetListAsync<AirportDto>(AirportsRoute, modifiedAfter, cancellationToken);

        private async Task<List<T>> GetListAsync<T>(string route, DateTimeOffset? modifiedAfter, CancellationToken cancellationToken)
        {
            var url = modifiedAfter is { } value
                ? $"{route}?modifiedAfter={Uri.EscapeDataString(value.ToString("o"))}"
                : route;

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<AirInfoEnvelope<List<T>>>(JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode || envelope?.Data is null)
                throw new AirInfoRequestException(FirstError(envelope) ?? $"AirInfo request '{route}' failed with status {(int)response.StatusCode}.");

            return envelope.Data;
        }

        private static string? FirstError<T>(AirInfoEnvelope<T>? envelope)
        {
            var error = envelope?.Errors?.FirstOrDefault();
            return error is null ? null : error.Detail ?? error.Title;
        }
    }
}
