using AeroTech.Ordering.Domain._Shared.Resources;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Offers;
using AeroTech.Ordering.Providers.Offer.Wire;

namespace AeroTech.Ordering.Providers.Offer.Services
{
    public sealed class OfferProvider : IOfferProvider
    {
        private const string OfferDetailRoute = "v1/FlightOffers/Details";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _httpClient;

        public OfferProvider(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<OfferDetail> GetByOfferIdAsync(string offerId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsJsonAsync(
                OfferDetailRoute,
                new { OfferId = offerId },
                JsonOptions,
                cancellationToken);

            var envelope = await response.Content
                .ReadFromJsonAsync<OfferEnvelope<FlightOfferDetailResponse>>(JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode || envelope?.Data is null)
                throw ExceptionFactory.OfferCouldNotBeRetrieved(FirstError(envelope), offerId);

            return OfferResponseMapper.ToDomain(envelope.Data);
        }

        private static string? FirstError(OfferEnvelope<FlightOfferDetailResponse>? envelope)
        {
            var error = envelope?.Errors?.FirstOrDefault();
            return error is null ? null : error.Detail ?? error.Title;
        }
    }
}
