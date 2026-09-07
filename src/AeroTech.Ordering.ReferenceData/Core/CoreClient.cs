using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AeroTech.Ordering.ReferenceData.Core.Wire;

namespace AeroTech.Ordering.ReferenceData.Core
{
    public sealed class CoreClient : ICoreClient
    {
        private const string CustomersRoute = "v1/Customers";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _httpClient;

        public CoreClient(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<List<CustomerDto>> GetCustomersAsync(DateTimeOffset? modifiedAfter, CancellationToken cancellationToken = default)
        {
            var url = modifiedAfter is { } value
                ? $"{CustomersRoute}?modifiedAfter={Uri.EscapeDataString(value.ToString("o"))}"
                : CustomersRoute;

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var envelope = await response.Content.ReadFromJsonAsync<CoreEnvelope<List<CustomerDto>>>(JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode || envelope?.Data is null)
                throw new CoreRequestException(FirstError(envelope) ?? $"Core request '{CustomersRoute}' failed with status {(int)response.StatusCode}.");

            return envelope.Data;
        }

        private static string? FirstError<T>(CoreEnvelope<T>? envelope)
        {
            var error = envelope?.Errors?.FirstOrDefault();
            return error is null ? null : error.Detail ?? error.Title;
        }
    }
}
