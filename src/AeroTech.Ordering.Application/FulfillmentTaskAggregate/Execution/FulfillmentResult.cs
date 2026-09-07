using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.Execution
{
    public sealed record FulfillmentResult(
        bool Success,
        string? Reference,
        DateTimeOffset? ExpiresAt,
        IReadOnlyList<FulfillmentTargetResult> Targets,
        string? Error,
        FulfillmentFailureKind? FailureKind,
        FulfillmentFailureReason? FailureReason,
        string? ProviderIdempotencyKey,
        string? RawRequest,
        string? RawResponse)
    {
        public static FulfillmentResult Succeeded(
            string? reference,
            DateTimeOffset? expiresAt,
            IReadOnlyList<FulfillmentTargetResult> targets,
            string? providerIdempotencyKey,
            string? rawRequest,
            string? rawResponse)
            => new(true, reference, expiresAt, targets, null, null, null, providerIdempotencyKey, rawRequest, rawResponse);

        public static FulfillmentResult Failed(
            string error,
            FulfillmentFailureKind failureKind,
            FulfillmentFailureReason failureReason,
            string? providerIdempotencyKey,
            string? rawRequest,
            string? rawResponse)
            => new(false, null, null, Array.Empty<FulfillmentTargetResult>(), error, failureKind, failureReason, providerIdempotencyKey, rawRequest, rawResponse);
    }

    public sealed record FulfillmentTargetResult(long OrderServiceId, string? ServiceReference);
}
