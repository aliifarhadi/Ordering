using System.Text.Json;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Ports.FlightFlow;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Adapters
{
    public sealed class AirlineReleaseInventoryAdapter : IFulfillmentProviderAdapter
    {
        private const string ReleaseIdempotencyKeyPrefix = "cancel-hold";

        private static readonly JsonSerializerOptions LogJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IFlightFlowProvider _flightFlowProvider;

        public AirlineReleaseInventoryAdapter(IFlightFlowProvider flightFlowProvider) => _flightFlowProvider = flightFlowProvider;

        public OrderProviderType ProviderType => OrderProviderType.Airline;

        public OrderFulfillmentTaskType TaskType => OrderFulfillmentTaskType.ReleaseReserved;

        public ProviderInteractionType InteractionType => ProviderInteractionType.ReleaseHold;

        public async Task<FulfillmentResult> ExecuteAsync(FulfillmentExecutionContext context, CancellationToken cancellationToken = default)
        {
            var idempotencyKey = $"{ReleaseIdempotencyKeyPrefix}:{context.Task.Id}";

            var holdId = context.Task.Targets
                .Select(target => target.FulfillmentReference)
                .FirstOrDefault(reference => !string.IsNullOrWhiteSpace(reference));

            if (string.IsNullOrWhiteSpace(holdId))
                return FulfillmentResult.Failed(
                    "The cancel task has no hold reference to release.",
                    FulfillmentFailureKind.Permanent,
                    FulfillmentFailureReason.ValidationFailed,
                    idempotencyKey,
                    null,
                    null);

            var request = new ReleaseHeldSeatsRequest(holdId);
            var rawRequest = JsonSerializer.Serialize(request, LogJsonOptions);

            try
            {
                var result = await _flightFlowProvider.ReleaseHeldAsync(request, cancellationToken);

                if (result.Released)
                    return FulfillmentResult.Succeeded(holdId, null, MapTargets(context), idempotencyKey, rawRequest, "200 OK");

                return FulfillmentResult.Failed(
                    result.Reason ?? "The seat hold could not be released.",
                    FulfillmentFailureKind.Permanent,
                    FulfillmentFailureReason.ProviderRejected,
                    idempotencyKey,
                    rawRequest,
                    result.Reason);
            }
            catch (ProviderRequestException exception)
            {
                return FulfillmentResult.Failed(exception.Message, exception.Kind, exception.Reason, idempotencyKey, rawRequest, exception.RawResponse);
            }
            catch (Exception exception)
            {
                return FulfillmentResult.Failed(
                    exception.Message,
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    idempotencyKey,
                    rawRequest,
                    null);
            }
        }

        private static IReadOnlyList<FulfillmentTargetResult> MapTargets(FulfillmentExecutionContext context)
            => context.Task.Targets
                .Where(target => target.OrderServiceId.HasValue)
                .Select(target => new FulfillmentTargetResult(target.OrderServiceId!.Value, null))
                .ToList();
    }
}
