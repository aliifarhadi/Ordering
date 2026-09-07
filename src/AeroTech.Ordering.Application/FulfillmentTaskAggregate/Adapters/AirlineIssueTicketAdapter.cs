using System.Text.Json;
using AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution;
using AeroTech.Ordering.Domain._Shared;
using AeroTech.Ordering.Domain.Providers.FlightFlow;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Adapters
{
    public sealed class AirlineIssueTicketAdapter : IFulfillmentProviderAdapter
    {
        private const string IssueIdempotencyKeyPrefix = "issue-ticket";

        private static readonly JsonSerializerOptions LogJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IFlightFlowProvider _flightFlowProvider;

        public AirlineIssueTicketAdapter(IFlightFlowProvider flightFlowProvider) => _flightFlowProvider = flightFlowProvider;

        public OrderProviderType ProviderType => OrderProviderType.Airline;

        public OrderFulfillmentTaskType TaskType => OrderFulfillmentTaskType.IssueTicket;

        public ProviderInteractionType InteractionType => ProviderInteractionType.ConfirmHold;

        public async Task<FulfillmentResult> ExecuteAsync(FulfillmentExecutionContext context, CancellationToken cancellationToken = default)
        {
            var issueIdempotencyKey = $"{IssueIdempotencyKeyPrefix}:{context.Task.Id}";

            var holdId = context.Task.Targets
                .Select(target => target.FulfillmentReference)
                .FirstOrDefault(reference => !string.IsNullOrWhiteSpace(reference));

            if (string.IsNullOrWhiteSpace(holdId))
                return FulfillmentResult.Failed(
                    "The issue task has no hold reference to confirm.",
                    FulfillmentFailureKind.Permanent,
                    FulfillmentFailureReason.ValidationFailed,
                    issueIdempotencyKey,
                    null,
                    null);

            var request = new ConfirmHoldRequest(holdId);
            var rawRequest = JsonSerializer.Serialize(request, LogJsonOptions);

            try
            {
                await _flightFlowProvider.ConfirmHoldAsync(request, cancellationToken);
            }
            catch (ProviderRequestException exception)
            {
                return FulfillmentResult.Failed(exception.Message, exception.Kind, exception.Reason, issueIdempotencyKey, rawRequest, exception.RawResponse);
            }
            catch (Exception exception)
            {
                return FulfillmentResult.Failed(
                    exception.Message,
                    FulfillmentFailureKind.Indeterminate,
                    FulfillmentFailureReason.UnknownOutcome,
                    issueIdempotencyKey,
                    rawRequest,
                    null);
            }

            return FulfillmentResult.Succeeded(holdId, null, MapTargets(context), issueIdempotencyKey, rawRequest, "204 No Content");
        }

        private static IReadOnlyList<FulfillmentTargetResult> MapTargets(FulfillmentExecutionContext context)
            => context.Task.Targets
                .Where(target => target.OrderServiceId.HasValue)
                .Select(target => new FulfillmentTargetResult(target.OrderServiceId!.Value, null))
                .ToList();
    }
}
