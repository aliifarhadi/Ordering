using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.DocumentRevalidation
{
    public sealed record DocumentRevalidationRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ProviderReference = null,
        string? Detail = null)
    {
        public DocumentRevalidationResult AsResult() => new(Outcome, ProviderReference, Detail);
    }
}
