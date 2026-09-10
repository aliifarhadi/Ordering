using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentRevalidation;

namespace AeroTech.Ordering.Providers.Deterministic
{
    public sealed class DeterministicDocumentRevalidationAdapter : IDocumentRevalidationPort
    {
        private readonly HashSet<string> _dispatched = new(StringComparer.Ordinal);

        public ProviderOperationOutcome RevalidationOutcome { get; set; } = ProviderOperationOutcome.Confirmed;

        public ProviderOperationOutcome RecoveryOutcome { get; set; } = ProviderOperationOutcome.Unknown;

        public bool ThrowBeforeDispatch { get; set; }

        public bool ThrowAfterDispatch { get; set; }

        public bool ThrowOnRecover { get; set; }

        public List<DocumentRevalidationRequest> ObservedRequests { get; } = new();

        public List<string> ObservedRecoveryKeys { get; } = new();

        public IReadOnlyCollection<string> DispatchedKeys => _dispatched;

        public Task<DocumentRevalidationResult> RevalidateAsync(
            DocumentRevalidationRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRequests.Add(request);

            if (ThrowBeforeDispatch)
                throw new InvalidOperationException("The revalidation request never left Ordering.");

            _dispatched.Add(request.OperationKey);

            if (ThrowAfterDispatch)
                throw new InvalidOperationException("The revalidation response never reached Ordering.");

            return Task.FromResult(new DocumentRevalidationResult(
                RevalidationOutcome,
                $"RVAL-{request.DocumentNumber}"));
        }

        public Task<DocumentRevalidationRecovery> RecoverAsync(
            DocumentRevalidationRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ObservedRecoveryKeys.Add(request.OperationKey);

            if (ThrowOnRecover)
                throw new InvalidOperationException("The revalidation provider is unreachable.");

            if (!_dispatched.Contains(request.OperationKey))
                return Task.FromResult(new DocumentRevalidationRecovery(
                    false, ProviderOperationOutcome.Unknown, Detail: "no such revalidation operation"));

            return Task.FromResult(new DocumentRevalidationRecovery(
                true,
                RecoveryOutcome,
                RecoveryOutcome == ProviderOperationOutcome.Confirmed ? $"RVAL-{request.DocumentNumber}" : null));
        }
    }
}
