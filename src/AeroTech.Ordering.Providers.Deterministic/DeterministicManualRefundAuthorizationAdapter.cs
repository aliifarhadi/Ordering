using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ManualRefundAuthorization;

namespace AeroTech.Ordering.Providers.Testing
{
    public sealed class DeterministicManualRefundAuthorizationAdapter : IManualRefundAuthorizationPort
    {
        public ManualRefundAuthorizationOutcome Outcome { get; set; } = ManualRefundAuthorizationOutcome.Denied;

        public string? DecisionReference { get; set; }

        public string? Detail { get; set; }

        public bool ThrowOnAuthorize { get; set; }

        public List<ManualRefundAuthorizationRequest> ObservedRequests { get; } = new();

        public Task<ManualRefundAuthorizationDecision> AuthorizeAsync(
            ManualRefundAuthorizationRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            if (ThrowOnAuthorize)
                throw new InvalidOperationException("The manual refund authorization authority is unreachable.");

            return Task.FromResult(new ManualRefundAuthorizationDecision(
                Outcome,
                Outcome == ManualRefundAuthorizationOutcome.Approved
                    ? DecisionReference ?? $"AUTHZ-{request.OperationId}"
                    : null,
                Detail));
        }
    }
}
