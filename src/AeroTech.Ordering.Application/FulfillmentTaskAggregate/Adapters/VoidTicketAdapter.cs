using System.Text.Json;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Providers.FlightFlow;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Adapters
{
    public sealed class VoidTicketAdapter : IFulfillmentProviderAdapter
    {
        private const string VoidIdempotencyKeyPrefix = "void-ticket";

        private static readonly JsonSerializerOptions LogJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IFlightFlowProvider _flightFlowProvider;

        public VoidTicketAdapter(IFlightFlowProvider flightFlowProvider) => _flightFlowProvider = flightFlowProvider;

        public OrderProviderType ProviderType => OrderProviderType.Airline;

        public OrderFulfillmentTaskType TaskType => OrderFulfillmentTaskType.CancelConfirmed;

        public ProviderInteractionType InteractionType => ProviderInteractionType.CancelConfirmed;

        public async Task<FulfillmentResult> ExecuteAsync(FulfillmentExecutionContext context, CancellationToken cancellationToken = default)
        {
            var idempotencyKey = $"{VoidIdempotencyKeyPrefix}:{context.Task.Id}";

            var reason = context.Task.CancellationReason ?? VoidReason.Other;

            var batches = context.Task.Targets
                .Where(target => !string.IsNullOrWhiteSpace(target.FulfillmentReference) && !string.IsNullOrWhiteSpace(target.ServiceReference))
                .GroupBy(target => target.FulfillmentReference!)
                .Select(group => new CancelConfirmedSeatsRequest(group.Key, group.Select(target => target.ServiceReference!).Distinct().ToList(), reason))
                .ToList();

            if (batches.Count == 0)
                return FulfillmentResult.Failed(
                    "The cancellation task has no hold batch and seat references to cancel.",
                    FulfillmentFailureKind.Permanent,
                    FulfillmentFailureReason.ValidationFailed,
                    idempotencyKey,
                    null,
                    null);

            var rawRequest = JsonSerializer.Serialize(batches, LogJsonOptions);

            try
            {
                foreach (var batch in batches)
                    await _flightFlowProvider.CancelConfirmedAsync(batch, cancellationToken);

                return FulfillmentResult.Succeeded(batches[0].HoldBatchId, null, MapTargets(context), idempotencyKey, rawRequest, "200 OK");
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
                .Select(target => new FulfillmentTargetResult(target.OrderServiceId!.Value, target.ServiceReference))
                .ToList();
    }
}
