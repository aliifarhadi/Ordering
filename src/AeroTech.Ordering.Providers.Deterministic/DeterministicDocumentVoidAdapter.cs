using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentVoid;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicDocumentVoidAdapter : IDocumentVoidPort
    {
        public EligibilityOutcome Eligibility { get; set; } = EligibilityOutcome.Allowed;

        public bool RefundRequiredInstead { get; set; }

        public string? EligibilityDetail { get; set; }

        public ProviderOperationOutcome VoidOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool ThrowBeforeEligibility { get; set; }

        public bool ThrowBeforeVoid { get; set; }

        public bool ThrowAfterVoid { get; set; }

        public List<string> ObservedEligibilityKeys { get; } = new();

        public List<string> ObservedVoidKeys { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public List<DocumentVoidEligibilityRequest> ObservedEligibilityRequests { get; } = new();

        public List<DocumentVoidRequest> ObservedVoidRequests { get; } = new();

        public List<DocumentVoidRecoveryRequest> ObservedRecoveryRequests { get; } = new();

        public Task<DocumentVoidEligibility> CheckEligibilityAsync(
            DocumentVoidEligibilityRequest request,
            CancellationToken cancellationToken = default)
        {
            if (ThrowBeforeEligibility)
                throw new InvalidOperationException("document void eligibility crashed before dispatch");

            ObservedEligibilityKeys.Add(request.OperationKey);
            ObservedEligibilityRequests.Add(request);

            return Task.FromResult(
                new DocumentVoidEligibility(Eligibility, RefundRequiredInstead, EligibilityDetail));
        }

        public Task<DocumentVoidResult> VoidAsync(
            DocumentVoidRequest request,
            CancellationToken cancellationToken = default)
        {
            if (ThrowBeforeVoid)
                throw new InvalidOperationException("document void crashed before dispatch");

            ObservedVoidKeys.Add(request.OperationKey);
            ObservedVoidRequests.Add(request);

            if (ThrowAfterVoid)
                throw new InvalidOperationException("document void crashed after the provider side effect");

            return Task.FromResult(new DocumentVoidResult(VoidOutcome, $"VOID-{request.DocumentNumber}"));
        }

        public Task<DocumentVoidResult> RecoverAsync(
            DocumentVoidRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);
            ObservedRecoveryRequests.Add(request);

            return Task.FromResult(
                new DocumentVoidResult(
                    RecoveryOutcome,
                    RecoveryOutcome == ProviderOperationOutcome.Confirmed
                        ? $"VOID-{request.DocumentNumber}"
                        : null));
        }
    }
}
