using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;

namespace AeroTech.Ordering.Domain.Ports.EmdExchange
{
    public sealed record EmdExchangeResult(
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        SuccessorEmdIdentity? Successor = null,
        string? Detail = null,
        ResidualDocumentIdentity? Residual = null);
}
