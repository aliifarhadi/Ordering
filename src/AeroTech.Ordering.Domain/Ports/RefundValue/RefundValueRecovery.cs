using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.RefundValue
{
    public sealed record RefundValueRecovery(
        bool WasDispatched,
        ProviderOperationOutcome Outcome,
        string? ValueMovementReference = null,
        string? Detail = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Disposition = null)
    {
        public RefundValueResult AsResult()
            => new(Outcome, ValueMovementReference, Detail, Amount, CurrencyId, Disposition);
    }
}
