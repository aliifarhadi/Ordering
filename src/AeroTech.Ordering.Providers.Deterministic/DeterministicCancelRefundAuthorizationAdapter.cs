using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.CancelRefundAuthorization;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicCancelRefundAuthorizationAdapter : ICancelRefundAuthorizationPort
    {
        public ManualRefundAuthorizationOutcome Outcome { get; set; } = ManualRefundAuthorizationOutcome.Denied;

        public string? DecisionReference { get; set; }

        public string? Detail { get; set; }

        public bool ThrowOnAuthorize { get; set; }

        public List<CancelRefundAuthorizationRequest> ObservedRequests { get; } = new();

        public Task<CancelRefundAuthorizationDecision> AuthorizeAsync(
            CancelRefundAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            if (ThrowOnAuthorize)
                throw new InvalidOperationException("The refund cancellation authorization authority is unreachable.");

            return Task.FromResult(new CancelRefundAuthorizationDecision(
                Outcome,
                Outcome == ManualRefundAuthorizationOutcome.Approved
                    ? DecisionReference ?? $"AUTHZ-CXRFND-{request.OperationId}"
                    : null,
                Detail));
        }
    }
}
