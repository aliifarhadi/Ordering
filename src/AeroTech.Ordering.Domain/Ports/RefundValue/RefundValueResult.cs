using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Ports.RefundValue
{
    public sealed record RefundValueResult(
        ProviderOperationOutcome Outcome,
        string? ValueMovementReference = null,
        string? Detail = null,
        decimal? Amount = null,
        int? CurrencyId = null,
        string? Disposition = null);
}
