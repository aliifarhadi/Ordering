using AeroTech.Ordering.Domain._Shared.Resources;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource;
using AeroTech.Ordering.Providers.Offer.Model;
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
        private readonly IAirPriceOfferNormalizer _normalizer;

        public OfferProvider(HttpClient httpClient, IAirPriceOfferNormalizer normalizer)
        {
            _httpClient = httpClient;
            _normalizer = normalizer;
        }

        public async Task<AcceptedOrderSource> GetAcceptedSourceAsync(string offerId, CancellationToken cancellationToken = default)
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

            return _normalizer.Normalize(OfferResponseMapper.ToProviderModel(envelope.Data));
        }

        private static string? FirstError(OfferEnvelope<FlightOfferDetailResponse>? envelope)
        {
            var error = envelope?.Errors?.FirstOrDefault();
            return error is null ? null : error.Detail ?? error.Title;
        }
    }
}
