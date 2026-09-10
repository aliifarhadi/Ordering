using AeroTech.Ordering.Domain._Shared.Resources;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AeroTech.Ordering.Domain.Ports.Pricing;
using AeroTech.Ordering.Providers.Pricing.Wire;

namespace AeroTech.Ordering.Providers.Pricing.Services
{
    public sealed class PricingProvider : IPricingProvider
    {
        private const string ReservationValidationRoute = "v1/BoundReservationValidation";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _httpClient;

        public PricingProvider(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<AirFareBoundReservationValidationResult> ReservationValidationAsync(
            AirFareBoundReservationValidationRequest request,
            CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync(ReservationValidationRoute, request, JsonOptions, cancellationToken);

            var envelope = await ReadEnvelopeAsync(response, cancellationToken);

            if (!response.IsSuccessStatusCode || envelope?.Data is null)
                throw ExceptionFactory.FareReservationCouldNotBeValidated(FirstError(envelope));

            return envelope.Data;
        }

        private static async Task<PricingEnvelope<AirFareBoundReservationValidationResult>?> ReadEnvelopeAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            try
            {
                return await response.Content.ReadFromJsonAsync<PricingEnvelope<AirFareBoundReservationValidationResult>>(JsonOptions, cancellationToken);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        private static string? FirstError(PricingEnvelope<AirFareBoundReservationValidationResult>? envelope)
        {
            var error = envelope?.Errors?.FirstOrDefault();
            return error is null ? null : error.Detail ?? error.Title;
        }
    }
}
