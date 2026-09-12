using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.EmdExchange;

namespace AeroTech.Ordering.Providers.Deterministic
{
    internal sealed record DeterministicEmdExchangeOperation(
        string Intent,
        ProviderOperationOutcome Outcome,
        string? ProviderReference,
        SuccessorEmdIdentity? Successor,
        ResidualDocumentIdentity? Residual)
    {
        public DeterministicEmdExchangeOperation Resolved(ProviderOperationOutcome recoveryOutcome)
            => Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected
                ? this
                : this with
                {
                    Outcome = recoveryOutcome,
                    ProviderReference = recoveryOutcome == ProviderOperationOutcome.Rejected
                        ? null
                        : ProviderReference,
                    Successor = recoveryOutcome == ProviderOperationOutcome.Confirmed ? Successor : null,
                    Residual = recoveryOutcome == ProviderOperationOutcome.Confirmed ? Residual : null
                };
    }
}
