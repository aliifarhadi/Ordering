using System.Text.Json;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Ports.FlightFlow;
using AeroTech.Messages.Ordering.Enums;
using Microsoft.Extensions.Options;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Adapters
{
    public sealed class AirlineReserveInventoryAdapter : IFulfillmentProviderAdapter
    {
        private const string HoldIdempotencyKeyPrefix = "reserve-hold";

        private static readonly JsonSerializerOptions LogJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IFlightFlowProvider _flightFlowProvider;
        private readonly IClock _clock;
        private readonly FulfillmentOptions _options;

        public AirlineReserveInventoryAdapter(
            IFlightFlowProvider flightFlowProvider,
            IClock clock,
            IOptions<FulfillmentOptions> options)
        {
            _flightFlowProvider = flightFlowProvider;
            _clock = clock;
            _options = options.Value;
        }

        public OrderProviderType ProviderType => OrderProviderType.Airline;

        public OrderFulfillmentTaskType TaskType => OrderFulfillmentTaskType.ReserveInventory;

        public ProviderInteractionType InteractionType => ProviderInteractionType.CreateHold;

        public async Task<FulfillmentResult> ExecuteAsync(FulfillmentExecutionContext context, CancellationToken cancellationToken = default)
        {
            var order = context.Order;
            var expiresAt = order.TimeToLive ?? _clock.GetDateTime().AddMinutes(_options.DefaultHoldMinutes);
            var holdIdempotencyKey = $"{HoldIdempotencyKeyPrefix}:{context.Task.Id}";
            var request = order.BuildReserveHoldRequest(holdIdempotencyKey, expiresAt);
            var rawRequest = JsonSerializer.Serialize(request, LogJsonOptions);

            FlightHeldSeatsResult hold;
            try
            {
                hold = await _flightFlowProvider.CreateHoldAsync(request, cancellationToken);
            }
            catch (ProviderRequestException exception)
            {
                return FulfillmentResult.Failed(exception.Message, exception.Kind, exception.Reason, holdIdempotencyKey, rawRequest, exception.RawResponse);
            }
            catch (Exception exception)
            {
                return FulfillmentResult.Failed(
                    exception.Message,
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    holdIdempotencyKey,
                    rawRequest,
                    null);
            }

            var rawResponse = JsonSerializer.Serialize(hold, LogJsonOptions);

            if (string.IsNullOrWhiteSpace(hold.HoldId))
                return FulfillmentResult.Failed(
                    "Inventory hold returned no hold id.",
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    holdIdempotencyKey,
                    rawRequest,
                    rawResponse);

            if (hold.ExpiresAt <= _clock.GetDateTime())
                return FulfillmentResult.Failed(
                    $"Inventory hold '{hold.HoldId}' has already expired (expired at {hold.ExpiresAt:o}).",
                    FulfillmentFailureKind.Permanent,
                    FulfillmentFailureReason.HoldExpired,
                    holdIdempotencyKey,
                    rawRequest,
                    rawResponse);

            return FulfillmentResult.Succeeded(hold.HoldId, hold.ExpiresAt, MapTargets(context, hold), holdIdempotencyKey, rawRequest, rawResponse);
        }

        private static IReadOnlyList<FulfillmentTargetResult> MapTargets(FulfillmentExecutionContext context, FlightHeldSeatsResult hold)
        {
            var order = context.Order;
            var results = new List<FulfillmentTargetResult>();

            foreach (var target in context.Task.Targets)
            {
                if (target.OrderServiceId is not { } orderServiceId)
                    continue;

                var service = order.OrderServices
                    .FirstOrDefault(candidate => candidate.Id == orderServiceId && candidate.IsAirTransport);
                if (service is null)
                    continue;

                var segment = order.Segments.FirstOrDefault(candidate => candidate.Id == service.SoldSegmentId);
                var seat = hold.Seats.FirstOrDefault(candidate =>
                    candidate.FlightId == segment?.FlightId.ToString()
                    && candidate.PaxReference == service.SoleBeneficiaryId.ToString());

                results.Add(new FulfillmentTargetResult(orderServiceId, seat?.SeatHoldReference));
            }

            return results;
        }
    }
}
