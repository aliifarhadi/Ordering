using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record DocumentExchangeRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        SuccessorDocumentIdentity? Successor = null,
        string? Detail = null,
        ResidualDocumentIdentity? Residual = null)
    {
        public DocumentExchangeResult AsResult() => new(Outcome, ProviderReference, Successor, Detail, Residual);
    }
}
