using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.ProviderInteractionAggregate
{
    public sealed class ProviderInteraction : AggregateRoot<long>
    {
        private ProviderInteraction()
        {
        }

        private ProviderInteraction(
            long id,
            long fulfillmentTaskId,
            long? fulfillmentTaskAttemptId,
            OrderProviderType providerType,
            string? supplierCode,
            ProviderInteractionType type,
            string idempotencyKey,
            string correlationId,
            string requestPayload)
        {
            Id = id;
            FulfillmentTaskId = fulfillmentTaskId;
            FulfillmentTaskAttemptId = fulfillmentTaskAttemptId;
            ProviderType = providerType;
            SupplierCode = supplierCode;
            Type = type;
            IdempotencyKey = idempotencyKey;
            CorrelationId = correlationId;
            RequestPayload = requestPayload;
            Status = ProviderInteractionStatus.Pending;
        }

        public long FulfillmentTaskId { get; private set; }

        public long? FulfillmentTaskAttemptId { get; private set; }

        public OrderProviderType ProviderType { get; private set; }

        public string? SupplierCode { get; private set; }

        public ProviderInteractionType Type { get; private set; }

        public string IdempotencyKey { get; private set; } = default!;

        public string CorrelationId { get; private set; } = default!;

        public string RequestPayload { get; private set; } = default!;

        public string? ResponsePayload { get; private set; }

        public ProviderInteractionStatus Status { get; private set; }

        public static ProviderInteraction Create(
            long id,
            long fulfillmentTaskId,
            long? fulfillmentTaskAttemptId,
            OrderProviderType providerType,
            string? supplierCode,
            ProviderInteractionType type,
            string idempotencyKey,
            string correlationId,
            string requestPayload)
            => new(id, fulfillmentTaskId, fulfillmentTaskAttemptId, providerType, supplierCode, type, idempotencyKey, correlationId, requestPayload);

        public void MarkSucceeded(string? responsePayload)
        {
            Status = ProviderInteractionStatus.Succeeded;
            ResponsePayload = responsePayload;
        }

        public void MarkFailed(string? responsePayload)
        {
            Status = ProviderInteractionStatus.Failed;
            ResponsePayload = responsePayload;
        }

        public void MarkTimedOut(string? responsePayload = null)
        {
            Status = ProviderInteractionStatus.TimedOut;
            ResponsePayload = responsePayload;
        }
    }
}
