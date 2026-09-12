using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentExchange
{
    public sealed record DocumentExchangeResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        SuccessorDocumentIdentity? Successor = null,
        string? Detail = null,
        ResidualDocumentIdentity? Residual = null);
}
