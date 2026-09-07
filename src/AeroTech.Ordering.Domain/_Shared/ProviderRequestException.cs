using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain._Shared
{
    public sealed class ProviderRequestException : Exception
    {
        public ProviderRequestException(
            FulfillmentFailureKind kind,
            FulfillmentFailureReason reason,
            string message,
            int? statusCode = null,
            string? rawResponse = null)
            : base(message)
        {
            Kind = kind;
            Reason = reason;
            StatusCode = statusCode;
            RawResponse = rawResponse;
        }

        public FulfillmentFailureKind Kind { get; }

        public FulfillmentFailureReason Reason { get; }

        public int? StatusCode { get; }

        public string? RawResponse { get; }
    }
}
