using AeroTech.Ordering.Domain._Shared.Resources;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Ports.FlightFlow;
using AeroTech.Ordering.Providers.FlightFlow.Wire;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Providers.FlightFlow.Services
{
    public sealed class FlightFlowProvider : IFlightFlowProvider
    {
        private const string SeatHoldsRoute = "v1/Flights/Seat-Holds";
        private const string SeatConfirmationsRoute = "v1/Flights/Seat-Confirmations";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private readonly HttpClient _httpClient;

        public FlightFlowProvider(HttpClient httpClient) => _httpClient = httpClient;

        public async Task<FlightHeldSeatsResult> CreateHoldAsync(HoldSeatsRequest request, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response;
            string body;

            try
            {
                response = await _httpClient.PostAsJsonAsync(SeatHoldsRoute, request, JsonOptions, cancellationToken);
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    "The seat hold request timed out.");
            }
            catch (HttpRequestException exception)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Retriable,
                    FulfillmentFailureReason.TechnicalFailed,
                    $"The seat hold request failed to reach FlightFlow. {exception.Message}");
            }

            using (response)
            {
                var envelope = Deserialize<FlightHeldSeatsResult>(body);

                if (!response.IsSuccessStatusCode)
                {
                    var (kind, reason) = Classify((int)response.StatusCode);
                    throw new ProviderRequestException(
                        kind,
                        reason,
                        FirstError(envelope) ?? $"The seat hold could not be created (HTTP {(int)response.StatusCode}): {Truncate(body)}",
                        (int)response.StatusCode,
                        body);
                }

                if (envelope?.Data is null)
                    throw new ProviderRequestException(
                        FulfillmentFailureKind.Indeterminate,
                        FulfillmentFailureReason.UnknownOutcome,
                        "The seat hold response contained no hold data.",
                        (int)response.StatusCode,
                        body);

                return envelope.Data;
            }
        }

        public async Task ExtendHeldAsync(ExtendHeldSeatsRequest request, CancellationToken cancellationToken = default)
        {
            var route = $"{SeatHoldsRoute}/{request.HoldBatchId}";
            var payload = new { expiresAt = request.ExpiresAt };

            HttpResponseMessage response;
            string body;

            try
            {
                response = await _httpClient.PatchAsJsonAsync(route, payload, JsonOptions, cancellationToken);
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    "The seat-hold extension timed out; the hold may or may not have been extended.");
            }
            catch (HttpRequestException exception)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Retriable,
                    FulfillmentFailureReason.TechnicalFailed,
                    $"The seat-hold extension failed to reach FlightFlow. {exception.Message}");
            }

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var (kind, reason) = Classify((int)response.StatusCode);
                    throw new ProviderRequestException(
                        kind,
                        reason,
                        $"The hold batch '{request.HoldBatchId}' could not be extended (HTTP {(int)response.StatusCode}): {Truncate(body)}",
                        (int)response.StatusCode,
                        body);
                }
            }
        }

        public async Task<SplitHeldSeatsResult> SplitHeldAsync(SplitHeldSeatsRequest request, CancellationToken cancellationToken = default)
        {
            var route = $"{SeatHoldsRoute}/{request.HoldBatchId}/Splits";
            var payload = new
            {
                idempotencyKey = request.IdempotencyKey,
                reference = request.Reference,
                expiresAt = request.ExpiresAt,
                moves = request.SeatHoldReferences.Select(seatHoldReference => new { seatHoldReference }).ToArray()
            };

            HttpResponseMessage response;
            string body;

            try
            {
                response = await _httpClient.PostAsJsonAsync(route, payload, JsonOptions, cancellationToken);
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    "The seat-hold split timed out; the seats may or may not have been split.");
            }
            catch (HttpRequestException exception)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Retriable,
                    FulfillmentFailureReason.TechnicalFailed,
                    $"The seat-hold split failed to reach FlightFlow. {exception.Message}");
            }

            using (response)
            {
                var envelope = Deserialize<FlightSplitSeatsResponse>(body);

                if (!response.IsSuccessStatusCode)
                {
                    var (kind, reason) = Classify((int)response.StatusCode);
                    throw new ProviderRequestException(
                        kind,
                        reason,
                        FirstError(envelope) ?? $"The hold batch '{request.HoldBatchId}' could not be split (HTTP {(int)response.StatusCode}): {Truncate(body)}",
                        (int)response.StatusCode,
                        body);
                }

                if (envelope?.Data is null)
                    throw new ProviderRequestException(
                        FulfillmentFailureKind.Indeterminate,
                        FulfillmentFailureReason.UnknownOutcome,
                        "The seat-hold split response contained no split data.",
                        (int)response.StatusCode,
                        body);

                return new SplitHeldSeatsResult(
                    envelope.Data.HoldId,
                    envelope.Data.ExpiresAt,
                    envelope.Data.Seats.Select(seat => seat.SeatHoldReference).ToList());
            }
        }

        public async Task ConfirmHoldAsync(ConfirmHoldRequest request, CancellationToken cancellationToken = default)
        {
            var route = $"{SeatHoldsRoute}/{request.HoldId}/Confirmations";

            HttpResponseMessage response;
            string body;

            try
            {
                response = await _httpClient.PostAsync(route, null, cancellationToken);
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    "The seat-hold confirmation timed out.");
            }
            catch (HttpRequestException exception)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Retriable,
                    FulfillmentFailureReason.TechnicalFailed,
                    $"The seat-hold confirmation failed to reach FlightFlow. {exception.Message}");
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                    return;

                var (kind, reason) = Classify((int)response.StatusCode);
                throw new ProviderRequestException(
                    kind,
                    reason,
                    FirstError(Deserialize<object>(body)) ?? $"The seat hold '{request.HoldId}' could not be confirmed (HTTP {(int)response.StatusCode}): {Truncate(body)}",
                    (int)response.StatusCode,
                    body);
            }
        }

        private static FlightSeatHoldCancellationReason MapCancellationReason(VoidReason reason) => reason switch
        {
            _ => FlightSeatHoldCancellationReason.PaxRequest
        };

        private static (FulfillmentFailureKind Kind, FulfillmentFailureReason Reason) Classify(int statusCode)
        {
            if (statusCode >= 500 || statusCode == 429)
                return (FulfillmentFailureKind.Retriable, FulfillmentFailureReason.TechnicalFailed);

            if (statusCode == 400)
                return (FulfillmentFailureKind.Permanent, FulfillmentFailureReason.BusinessRejected);

            return (FulfillmentFailureKind.Permanent, FulfillmentFailureReason.ProviderRejected);
        }

        public async Task<ReleaseHeldSeatsResult> ReleaseHeldAsync(ReleaseHeldSeatsRequest request, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"{SeatHoldsRoute}/{request.HoldId}", cancellationToken);

            if (response.IsSuccessStatusCode)
                return new ReleaseHeldSeatsResult(true, null);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var reason = FirstError(Deserialize<object>(body)) ?? $"HTTP {(int)response.StatusCode}: {Truncate(body)}";

            if (response.StatusCode == HttpStatusCode.BadRequest)
                return new ReleaseHeldSeatsResult(false, reason);

            throw ExceptionFactory.SeatHoldCouldNotBeReleased(request.HoldId, reason);
        }

        public async Task<CancelConfirmedSeatsResult> CancelConfirmedAsync(CancelConfirmedSeatsRequest request, CancellationToken cancellationToken = default)
        {
            var route = $"{SeatConfirmationsRoute}/{request.HoldBatchId}/Cancellation";
            var payload = new { reasonCode = (int)MapCancellationReason(request.Reason), seatHoldReferences = request.SeatHoldReferences };

            HttpResponseMessage response;
            string body;

            try
            {
                response = await _httpClient.PostAsJsonAsync(route, payload, JsonOptions, cancellationToken);
                body = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    "The seat cancellation timed out; the seats may or may not have been released.");
            }
            catch (HttpRequestException exception)
            {
                throw new ProviderRequestException(
                    FulfillmentFailureKind.Retriable,
                    FulfillmentFailureReason.TechnicalFailed,
                    $"The seat cancellation failed to reach FlightFlow. {exception.Message}");
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                    return new CancelConfirmedSeatsResult(true, null);

                var (kind, reason) = Classify((int)response.StatusCode);
                throw new ProviderRequestException(
                    kind,
                    reason,
                    FirstError(Deserialize<object>(body)) ?? $"The confirmed seats for hold batch '{request.HoldBatchId}' could not be cancelled (HTTP {(int)response.StatusCode}): {Truncate(body)}",
                    (int)response.StatusCode,
                    body);
            }
        }

        private static FlightFlowEnvelope<T>? Deserialize<T>(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            try
            {
                return JsonSerializer.Deserialize<FlightFlowEnvelope<T>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string? FirstError<T>(FlightFlowEnvelope<T>? envelope)
        {
            var error = envelope?.Errors?.FirstOrDefault();
            return error is null ? null : error.Detail ?? error.Title;
        }

        private static string Truncate(string value)
            => string.IsNullOrEmpty(value) ? "<empty>" : value.Length <= 1000 ? value : value[..1000];
    }
}
